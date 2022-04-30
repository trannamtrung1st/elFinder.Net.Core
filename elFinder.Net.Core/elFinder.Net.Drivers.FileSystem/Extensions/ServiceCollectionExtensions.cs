using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileSystemDriver(this IServiceCollection services,
            Type driverType = null,
            Action<TempFileCleanerOptions> tempFileCleanerConfig = null)
        {
            if (tempFileCleanerConfig == null)
                tempFileCleanerConfig = (opt) => { };

            return services.AddScoped(typeof(IDriver), driverType ?? typeof(FileSystemDriver))
                .AddSingleton<IZipDownloadPathProvider, TempZipDownloadPathProvider>()
                .AddSingleton<IZipFileArchiver, ZipFileArchiver>()
                .AddSingleton<IThumbnailBackgroundGenerator, DefaultThumbnailBackgroundGenerator>()
                .AddSingleton<ICryptographyProvider, DefaultCryptographyProvider>()
                .AddSingleton<ITempFileCleaner, DefaultTempFileCleaner>()
                .Configure(tempFileCleanerConfig);
        }
    }
}
