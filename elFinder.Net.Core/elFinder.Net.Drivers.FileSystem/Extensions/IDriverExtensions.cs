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
            driver.OnAfterUpload.Add(async (file, destFile, formFile, isOverwrite, isChunking) =>
            {
                MediaType? mediaType = null;

                if ((mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) == null) return;
                await file.RefreshAsync(cancellationToken: cancellationToken);
                var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
            });

            driver.OnAfterCopy.Add(async (fileSystem, newFileSystem, isOverwrite) =>
            {
                MediaType? mediaType = null;
                if (newFileSystem is IFile file && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync(cancellationToken: cancellationToken);
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            });

            driver.OnAfterExtractFile.Add(async (entry, destFile, isOverwrite) =>
            {
                MediaType? mediaType = null;

                if ((mediaType = destFile.CanGetThumb(pictureEditor, videoEditor, verify: false)) == null) return;

                await destFile.RefreshAsync(cancellationToken: cancellationToken);
                var tmbFilePath = await driver.GenerateThumbPathAsync(destFile, cancellationToken: cancellationToken);
                var tmbFile = driver.CreateFile(tmbFilePath, destFile.Volume);
                generator.TryAddToQueue(destFile, tmbFile, destFile.Volume.ThumbnailSize, keepRatio, mediaType);
            });

            driver.OnAfterMove.Add(async (fileSystem, newFileSystem, isOverwrite) =>
            {
                MediaType? mediaType = null;
                if (newFileSystem is IFile file
                    && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync(cancellationToken: cancellationToken);
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            });

            driver.OnAfterRename.Add(async (IFileSystem fileSystem, string prevName) =>
            {
                MediaType? mediaType = null;
                if (fileSystem is IFile file && (mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify: false)) != null)
                {
                    await file.RefreshAsync(cancellationToken: cancellationToken);
                    var tmbFilePath = await driver.GenerateThumbPathAsync(file, cancellationToken: cancellationToken);
                    var tmbFile = driver.CreateFile(tmbFilePath, file.Volume);
                    generator.TryAddToQueue(file, tmbFile, file.Volume.ThumbnailSize, keepRatio, mediaType);
                }
            });
        }
    }
}
