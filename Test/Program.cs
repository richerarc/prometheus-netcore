using System;
using System.Threading;
using Prometheus;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            MetricServer server = new MetricServer(4492);

            var gauge = Metrics.CreateGauge("gauge1", "");
            
            server.Start();
            
            Console.WriteLine("You can use prometheus metric server to visualize the data");

            for (var i = 0; i < 1000; i++)
            {
                gauge.Inc();
                Thread.Sleep(10);
            }
            
            Console.WriteLine("Press enter to quit");
            Console.ReadLine();
            
            server.Stop();
        }
    }
}
