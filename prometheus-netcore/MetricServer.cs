using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices.WindowsRuntime;
using Prometheus.Advanced;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
    public class MetricServer : MetricHandler
    {
        private IDisposable _schedulerDelegate;
        private readonly string hostAddress;
        private readonly int port;
        private readonly string url;
        private IWebHost host;
        public bool IsRunning => host != null;
        private X509Certificate2 certificate;
        
        private Action<IApplicationBuilder> configure;

        public MetricServer(int port, IEnumerable<IOnDemandCollector> standardCollectors = null,
            string url = "/metrics", ICollectorRegistry registry = null,
            bool useHttps = false, X509Certificate2 certificate = null) :
            this("+", port, standardCollectors, url, registry, useHttps)
        {
        }

        public MetricServer(string hostname, int port, IEnumerable<IOnDemandCollector> standardCollectors = null,
            string url = "/metrics", ICollectorRegistry registry = null,
            bool useHttps = false, X509Certificate2 certificate = null) : base(standardCollectors, registry)
        {
            if (useHttps && certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate),
                    $"{nameof(certificate)} is required when using https");
            }
            this.certificate = certificate;
            var s = useHttps ? "s" : "";
            this.port = port;
            this.url = url;
            hostAddress = $"http{s}://{hostname}:{port}/{url}";
            if (_registry == DefaultCollectorRegistry.Instance)
            {
                // Default to DotNetStatsCollector if none speified
                // For no collectors, pass an empty collection
                if (standardCollectors == null)
                    standardCollectors = new[] {new DotNetStatsCollector()};

                DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(standardCollectors);
            }
        }

        protected override IDisposable StartLoop(IScheduler scheduler)
        {
            if (host != null)
            {
                throw new Exception("Server is already running.");
            }

            configure += Configure;
            
            var configBuilder = new ConfigurationBuilder();
            configBuilder.Properties["parent"] = this;
            var config = configBuilder.Build();

            host = new WebHostBuilder()
                //.UseConfiguration(config)
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, port, listenOptions =>
                    {
                        if (certificate != null)
                        {
                            listenOptions.UseHttps(certificate);
                        }
                    });
                })
                .Configure(configure)
                .Build();

            host.Start();

            return host;
        }

        private void Configure(IApplicationBuilder app)
        {
            app.Map(url, metric =>
            {
                metric.Run(context =>
                {
                    var response = context.Response;
                    var request = context.Request;
                    response.StatusCode = 200;

                    var acceptHeader = request.Headers["Accept"];
                    var contentType = ScrapeHandler.GetContentType(acceptHeader);
                    response.ContentType = contentType;

                    using (var outputStream = response.Body)
                    {
                        var collected = _registry.CollectAll();
                        ScrapeHandler.ProcessScrapeRequest(collected, contentType, outputStream);
                    }
                    return Task.FromResult(true);
                });
            });
            app.Run(async context =>
            {
                await context.Response.WriteAsync("sup");
            });
        }

        protected override void StopInner()
        {
            if (host != null)
            {
                host.Dispose();
                host = null;
            }
        }

//        public class Startup : IStartup
//        {
//            private readonly ICollectorRegistry _registry;
//            private readonly string path;
//            public IConfigurationRoot Configuration { get; }
//
//            public Startup(ICollectorRegistry _registry, string path)
//            {
//                this._registry = _registry;
//                var builder = new ConfigurationBuilder();
//                Configuration = builder.Build();
//                this.path = path;
//            }
//
//            public IServiceProvider ConfigureServices(IServiceCollection services)
//            {
//                return services.BuildServiceProvider();
//            }
//
//            public void Configure(IApplicationBuilder app)
//            {
//                app.Map(path, metric =>
//                {
//                    metric.Run(context =>
//                    {
//                        var response = context.Response;
//                        var request = context.Request;
//                        response.StatusCode = 200;
//
//                        var acceptHeader = request.Headers["Accept"];
//                        var contentType = ScrapeHandler.GetContentType(acceptHeader);
//                        response.ContentType = contentType;
//
//                        using (var outputStream = response.Body)
//                        {
//                            var collected = _registry.CollectAll();
//                            ScrapeHandler.ProcessScrapeRequest(collected, contentType, outputStream);
//                        }
//                        ;
//                        return Task.FromResult(true);
//                    });
//                });
//            }
//        }
    }
}