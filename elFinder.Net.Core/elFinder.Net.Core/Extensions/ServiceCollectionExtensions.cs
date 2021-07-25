using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Plugins;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;

namespace elFinder.Net.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection Capture<T>(this IServiceCollection services, ServiceLifetime lifetime, Func<IServiceProvider, T, T> captureFunc)
            where T : class
        {
            return services.Capture<T, T>(lifetime, captureFunc);
        }

        public static IServiceCollection Capture<In, Out>(this IServiceCollection services, ServiceLifetime lifetime, Func<IServiceProvider, In, Out> captureFunc)
            where In : class where Out : class
        {
            services.Add(new ServiceDescriptor(typeof(Out), DIHelper.Capture(captureFunc), lifetime));

            return services;
        }

        public static IServiceCollection AddElFinderCore(this IServiceCollection services,
            Action<ConnectorManagerOptions> connectorManagerConfig = null)
        {
            if (connectorManagerConfig == null)
                connectorManagerConfig = (options) =>
                {
                    options.MaximumItems = ConnectorManagerOptions.DefaultMaximumItems;
                    options.TokenSourceCachingLifeTime = ConnectorManagerOptions.DefaultCcTokenSourceCachingLifeTime;
                    options.LockCachingLifeTime = ConnectorManagerOptions.DefaultLockCachingLifeTime;
                    options.PollingInterval = ConnectorManagerOptions.DefaultPollingInterval;
                };

            services.AddSingleton<IPathParser, PathParser>()
                .AddSingleton<IPictureEditor, DefaultPictureEditor>()
                .AddSingleton<IVideoEditor, DefaultVideoEditor>()
                .AddSingleton<IConnectorManager, ConnectorManager>()
                .Configure(connectorManagerConfig);

            return services.AddScoped<IConnector, Connector>()
                .AddScoped<ConnectorOptions>()
                .AddScoped<PluginManager>();
        }

        public static IServiceCollection AddElFinderPlugins(this IServiceCollection services, PluginCollection pluginCollection)
        {
            if (pluginCollection == null) throw new ArgumentNullException(nameof(pluginCollection));

            var groups = pluginCollection.Captures.GroupBy(o => new { o.Type, o.ImplType }).ToArray();

            foreach (var group in groups)
            {
                var currentDescriptor = services.FirstOrDefault(des => des.ServiceType == group.Key.Type
                    && des.ImplementationType == group.Key.ImplType);

                if (currentDescriptor == null) continue;

                services.Replace(new ServiceDescriptor(group.Key.Type,
                    DIHelper.Capture(group.Key.ImplType, (provider, service) =>
                    {
                        foreach (var capture in group)
                        {
                            service = capture.CaptureFunc(provider, service);
                        }

                        return service;
                    }), currentDescriptor.Lifetime));
            }

            return services;
        }
    }
}
