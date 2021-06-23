using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Plugins;
using elFinder.Net.Drivers.FileSystem;
using elFinder.Net.Plugins.LoggingExample.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Plugins.LoggingExample.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElFinderLoggingExample(this IServiceCollection services, PluginCollection collection,
            Type connectorType = null,
            Type driverType = null)
        {
            services.AddScoped<LoggingInterceptor>();

            collection.Captures.Add(new PluginCapture
            {
                ImplType = connectorType ?? typeof(Connector),
                Type = typeof(IConnector),
                CaptureFunc = (provider, service) =>
                {
                    var interceptor = provider.GetRequiredService<LoggingInterceptor>();
                    var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget(service as IConnector, interceptor);
                    return proxy;
                }
            });

            collection.Captures.Add(new PluginCapture
            {
                ImplType = driverType ?? typeof(FileSystemDriver),
                Type = typeof(IDriver),
                CaptureFunc = (provider, service) =>
                {
                    var interceptor = provider.GetRequiredService<LoggingInterceptor>();
                    var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget(service as IDriver, interceptor);
                    return proxy;
                }
            });

            return services;
        }
    }
}
