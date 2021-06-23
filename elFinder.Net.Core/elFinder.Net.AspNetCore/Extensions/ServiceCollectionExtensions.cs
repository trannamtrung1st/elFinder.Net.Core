using elFinder.Net.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace elFinder.Net.AspNetCore.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElFinderAspNetCore(this IServiceCollection services)
        {
            return services.AddElFinderCore();
        }
    }
}
