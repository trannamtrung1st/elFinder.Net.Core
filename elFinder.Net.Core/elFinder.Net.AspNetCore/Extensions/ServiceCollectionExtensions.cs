using elFinder.Net.Core;
using elFinder.Net.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.AspNetCore.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElFinderAspNetCore(this IServiceCollection services, Action<IConnector> config = null)
        {
            return services.AddElFinderCore(config);
        }
    }
}
