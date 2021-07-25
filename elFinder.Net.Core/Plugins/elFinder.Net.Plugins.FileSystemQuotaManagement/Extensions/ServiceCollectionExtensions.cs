using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Plugins;
using elFinder.Net.Drivers.FileSystem;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileSystemQuotaManagement(this IServiceCollection services, PluginCollection collection,
            Action<StorageManagerOptions> storageManagerOptionsConfig = null,
            Type fileSystemDriverImplType = null,
            Type connectorOptionsType = null)
        {
            if (storageManagerOptionsConfig == null)
                storageManagerOptionsConfig = (options) =>
                {
                    options.StorageCachingLifeTime = StorageManagerOptions.DefaultStorageCachingLifeTime;
                    options.MaximumItems = StorageManagerOptions.DefaultMaximumItems;
                    options.ReservationsAfterCleanUp = StorageManagerOptions.DefaultReservationsAfterCleanUp;
                    options.PollingInterval = StorageManagerOptions.DefaultPollingInterval;
                };

            services.AddScoped<DriverInterceptor>()
                .AddScoped<ConnectorOptionsInterceptor>()
                .AddSingleton<IStorageManager, StorageManager>()
                .AddScoped<QuotaManagementContext>()
                .Configure(storageManagerOptionsConfig);

            collection.Captures.Add(new PluginCapture
            {
                ImplType = fileSystemDriverImplType ?? typeof(FileSystemDriver),
                Type = typeof(IDriver),
                CaptureFunc = (provider, driver) =>
                {
                    var interceptor = provider.GetRequiredService<DriverInterceptor>();
                    var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget(driver as IDriver, interceptor);
                    return proxy;
                }
            });

            collection.Captures.Add(new PluginCapture
            {
                ImplType = connectorOptionsType ?? typeof(ConnectorOptions),
                Type = typeof(ConnectorOptions),
                CaptureFunc = (provider, options) =>
                {
                    var interceptor = provider.GetRequiredService<ConnectorOptionsInterceptor>();
                    var proxy = new ProxyGenerator().CreateClassProxyWithTarget(options as ConnectorOptions, interceptor);
                    return proxy;
                }
            });

            return services;
        }
    }
}
