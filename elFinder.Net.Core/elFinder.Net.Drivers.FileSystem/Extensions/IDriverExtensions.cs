using elFinder.Net.Core;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Services;
using System.Threading;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class IDriverExtensions
    {
        public static void SetupBackgroundThumbnailGenerator(this IDriver driver,
            IThumbnailBackgroundGenerator generator, IPictureEditor pictureEditor, IVideoEditor videoEditor,
            bool keepRatio = true, CancellationToken cancellationToken = default)
        {
            driver.OnAfterUpload += async (sender, args) =>
            {
                var file = args.File;
                MediaType? mediaType = null;

                if ((mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) == null) return;
                await file.RefreshAsync();
                var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
            };

            driver.OnAfterCopy += async (sender, args) =>
            {
                MediaType? mediaType = null;
                if (args.NewFileSystem is IFile file && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync();
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            };

            driver.OnAfterExtractFile += async (sender, args) =>
            {
                var file = args.DestFile;
                MediaType? mediaType = null;

                if ((mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) == null) return;

                await file.RefreshAsync();
                var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
            };

            driver.OnAfterMove += async (sender, args) =>
            {
                MediaType? mediaType = null;
                if (args.NewFileSystem is IFile file && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync();
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            };

            driver.OnAfterRename += async (sender, args) =>
            {
                MediaType? mediaType = null;
                if (args.FileSystem is IFile file && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync();
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            };
        }
    }
}
