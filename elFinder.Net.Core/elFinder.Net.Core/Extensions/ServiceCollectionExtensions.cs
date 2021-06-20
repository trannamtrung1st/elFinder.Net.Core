using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElFinderCore(this IServiceCollection services, Action<IConnector> config = null,
            Action<ConnectorManagerOptions> connectorManagerConfig = null)
        {
            if (connectorManagerConfig == null)
                connectorManagerConfig = (options) =>
                {
                    options.MaximumItems = ConnectorManagerOptions.DefaultMaximumItems;
                    options.TokenSourceCachingMinutes = ConnectorManagerOptions.DefaultCcTokenSourceCachingMinutes;
                    options.PollingIntervalInMinutes = ConnectorManagerOptions.DefaultPollingIntervalInMinutes;
                };

            services.AddSingleton<IPathParser, PathParser>()
               .AddSingleton<IPictureEditor, DefaultPictureEditor>()
               .AddSingleton<IConnectorManager, ConnectorManager>()
               .Configure(connectorManagerConfig);

            if (config == null) return services.AddScoped<IConnector, Connector>();

            var factory = ActivatorUtilities.CreateFactory(typeof(Connector), new Type[0]);
            return services.AddScoped(provider =>
            {
                var connector = factory(provider, default) as IConnector;
                config(connector);
                return connector;
            });
        }
    }
}
