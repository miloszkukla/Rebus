using Rebus.Log4Net;
using Rebus.Logging;
using Topshelf;
using log4net.Config;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;

namespace Rebus.HttpGateway
{
    class Program
    {
        static void Main()
        {
            XmlConfigurator.Configure();

            RebusLoggerFactory.Current = new Log4NetLoggerFactory();

            HostFactory
                .Run(s =>
                {
                    const string text = "Rebus Gateway Service";

                    s.UseLog4Net();
                    s.SetDescription("Rebus Gateway Service - Install named instance by adding '/instance:\"myInstance\"' when installing.");
                    s.SetDisplayName(text);
                    s.SetInstanceName("default");
                    s.SetServiceName("rebus_gateway_service");

                    s.Service<GatewayService>(c =>
                    {
                        c.ConstructUsing(GetGatewayServiceInstance);
                        c.WhenStarted(t => t.Start());
                        c.WhenStopped(t => t.Stop());
                    });
                });
        }

        public static void UpdateConfiguration(HttpGatewayConfiguration configuration)
        {
            var stream = System.IO.File.Open(GetConfigFilePath(), System.IO.FileMode.Create);
            new XmlSerializer(typeof(HttpGatewayConfiguration)).Serialize(stream, configuration);
            stream.Close();
        }

        public static string GetConfigFilePath() //temporary method
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(path, "Gateway.config");
        }
        public static HttpGatewayConfiguration GetConfig() //temporary method
        {
            //when I will upgrade gateway's configuration class (System.Configuration.ConfigurationSection based one)
            //then configuration should be located automatically. until that time I create configPath myself
            var stream = File.OpenRead(GetConfigFilePath());
            var cfg = (HttpGatewayConfiguration)new XmlSerializer(typeof(HttpGatewayConfiguration)).Deserialize(stream);
            stream.Close();
            return cfg;
        }

        static GatewayService GetGatewayServiceInstance()
        {
            //var cfg = RebusGatewayConfigurationSection.LookItUp();

            var cfg = GetConfig();
            var gateway = new GatewayService(cfg);

            //if (cfg.Inbounds != null)
            //{
            //    gateway.ListenUri = cfg.Inbound.ListenUri;
            //    gateway.DestinationQueue = cfg.Inbound.DestinationQueue;
            //}

            //if (cfg.Outbounds != null)
            //{
            //    gateway.DestinationUri = cfg.Outbound.DestinationUri;
            //    gateway.ListenQueue = cfg.Outbound.ListenQueue;
            //}

            //cfg.Outboud.ErrorQueue is not used in Mogen's code !!!

            return gateway;
        }
    }

    public class HttpGatewayConfiguration
    {
        public List<InboundConfiguration> Inbounds { get; set; }
        public List<OutboundConfiguration> Outbounds { get; set; }
        public bool EnableConfigurationHotSwap { get; set; }

        public class InboundConfiguration
        {
            public string ListenUri { get; set; }
            public string DestinationQueue { get; set; }
        }

        public class OutboundConfiguration
        {
            public string ListenQueue { get; set; }
            public string ErrorQueue { get; set; }
            public string DestinationUri { get; set; }
        }
    }
}
