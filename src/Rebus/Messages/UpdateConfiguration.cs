using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rebus.Messages
{
    [Serializable]
    public class UpdateConfiguration<TConfiguration>
    {
        public TConfiguration NewConfiguration { get; private set; }

        public UpdateConfiguration(TConfiguration newConfiguration)
        {
            this.NewConfiguration = newConfiguration;
        }
    }

    [Serializable]
    public class GiveMeYourConfigurationRequest<TConfiguration>
    {

    }

    [Serializable]
    public class GiveMeYourConfigurationResponse<TConfiguration>
    {
        public TConfiguration Configuration { get; private set; }

        public GiveMeYourConfigurationResponse(TConfiguration configuration)
        {
            this.Configuration = configuration;
        }
    }
}
