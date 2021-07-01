using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Services;
using Microsoft.Extensions.DependencyInjection;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileSystemDriver(this IServiceCollection services)
        {
            return services.AddScoped<IDriver, FileSystemDriver>()
                .AddSingleton<IZipDownloadPathProvider, TempZipDownloadPathProvider>()
                .AddSingleton<IZipFileArchiver, ZipFileArchiver>()
                .AddSingleton<IThumbnailBackgroundGenerator, DefaultThumbnailBackgroundGenerator>();
        }
    }
}
