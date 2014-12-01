using System;
using Rebus.HttpGateway.Inbound;
using Rebus.HttpGateway.Outbound;
using Rebus.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Rebus.HttpGateway
{
    public class GatewayService
    {
        static ILog log;

        static GatewayService()
        {
            RebusLoggerFactory.Changed += f => log = f.GetCurrentClassLogger();
        }


        HttpGatewayConfiguration configuration;

        List<InboundService> inboundServices;
        List<OutboundService> outboundServices;

        #region Old stuff left only to compile tests until they are changed
        public string ListenQueue { get; set; }
        public string DestinationUri { get; set; }

        public string DestinationQueue { get; set; }
        public string ListenUri { get; set; }
        #endregion

        public GatewayService() 
        {
            throw new Exception("This is fake temporary constructor allowing tests to compile until they are changed");
        }

        public GatewayService(HttpGatewayConfiguration configuration)
        {
            this.configuration = configuration;
            this.inboundServices = new List<InboundService>();
            this.outboundServices = new List<OutboundService>();
        }

        public void Start()
        {
            if (!HasInboundConfiguration() && !HasOutboundConfiguration())
            {
                throw new InvalidOperationException(string.Format(@"
Cannot start the gateway, since ListenUri and ListenQueue are both empty!

You need to equip the gateway with enough information to at least work in
one-way mode. Available modes are described here:

{0}", GenericHelpText()));
            }

            if (HasInboundConfiguration())
            {
                InitHttpListeners();
            }
            else
            {
                log.Info("No listen URI has been configured - gateway service is running in one-way mode...");
            }

            if (HasOutboundConfiguration())
            {
                InitQueueListeners();
            }
            else
            {
                log.Info("No listen queue name has been configured - gateway service is running in one-way mode...");
            }

            log.Info("Started!");
        }

        bool HasOutboundConfiguration()
        {
            //return !string.IsNullOrEmpty(ListenQueue);
            //why DestinationUri is not chcecked??

            return configuration.Outbounds != null && configuration.Outbounds.Count > 0
                && configuration.Outbounds.All(x => !string.IsNullOrEmpty(x.ListenQueue) && !string.IsNullOrEmpty(x.DestinationUri));
        }

        bool HasInboundConfiguration()
        {
            //return !string.IsNullOrEmpty(ListenUri);
            //why DestinationQueue is not checked??

            return configuration.Inbounds != null && configuration.Outbounds.Count > 0
                && configuration.Inbounds.All(x => !string.IsNullOrEmpty(x.DestinationQueue) && !string.IsNullOrEmpty(x.ListenUri));
        }

        string GenericHelpText()
        {
            return @"
The gateway can work in one of three modes: inbound, outbound, or full duplex.

    Inbound:
        In this mode, the gateway has an HTTP endpoint that listens to incoming
        messages, which are then put in a queue. In this mode, a ListenUri and
        a DestinationQueue must be configured.

    Outbound:
        In this mode, the gateway receives messages out of a queue, which are
        then sent to another gateway with an HTTP request. In this mode, a
        ListenQueue and a DestinationUri must be configured.

    Full Duplex:
        In this mode, the gateway works in a combined inbound/outbound mode,
        and thus all the parameters of both inbound and outbound modes must
        be configured.
";
        }

        void InitQueueListeners()
        {
            int i = 0;
            foreach (var outbound in configuration.Outbounds)
            {
                i++;
                log.Info(string.Format("Starting outbound service #{0}...", i));
                var outboundService = new OutboundService(outbound.ListenQueue, outbound.DestinationUri);
                outboundServices.Add(outboundService);
                outboundService.Start();
            }
        }

        void InitHttpListeners()
        {
            int i = 0;
            foreach (var inbound in configuration.Inbounds)
            {
                i++;
                log.Info(string.Format("Starting inbound service #{0}...", i));
                var inboundService = new InboundService(inbound.ListenUri, inbound.DestinationQueue);
                inboundServices.Add(inboundService);
                inboundService.Start();
            }
        }

        public void Stop()
        {
            int i = 0;
            foreach (var inboundService in inboundServices)
            {
                i++;
                log.Info(string.Format("Stopping inbound service #{0}...", i));
                inboundService.Stop();
            }

            int j = 0;
            foreach (var outboundService in outboundServices)
            {
                j++;
                log.Info(string.Format("Stopping outbound service #{0}...", j));
                outboundService.Stop();
            }
        }
    }
}