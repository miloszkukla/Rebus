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

        static GatewayService GetGatewayServiceInstance()
        {
            //var cfg = RebusGatewayConfigurationSection.LookItUp();
            //przy używaniu settingsów .netowych to konfig w katalogu binarki powinien być znaleziony automatycznie
            //a póki sam wczytuje konfiguracje muszę podać pełną scieżkę
            var path = Path.Combine(Assembly.GetExecutingAssembly().CodeBase, "Gateway.config");
            var stream = File.OpenRead(path);
            var cfg = (HttpGatewayConfiguration)new XmlSerializer(typeof(HttpGatewayConfiguration)).Deserialize(stream);

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
    }

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
