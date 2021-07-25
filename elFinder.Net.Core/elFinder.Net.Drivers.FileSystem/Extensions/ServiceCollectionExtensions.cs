using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileSystemDriver(this IServiceCollection services,
            Action<TempFileCleanerOptions> tempFileCleanerConfig = null)
        {
            if (tempFileCleanerConfig == null)
                tempFileCleanerConfig = (opt) => { };

            return services.AddScoped<IDriver, FileSystemDriver>()
                .AddSingleton<IZipDownloadPathProvider, TempZipDownloadPathProvider>()
                .AddSingleton<IZipFileArchiver, ZipFileArchiver>()
                .AddSingleton<IThumbnailBackgroundGenerator, DefaultThumbnailBackgroundGenerator>()
                .AddSingleton<ICryptographyProvider, DefaultCryptographyProvider>()
                .AddSingleton<ITempFileCleaner, DefaultTempFileCleaner>()
                .Configure(tempFileCleanerConfig);
        }
    }
}
