using elFinder.Net.Core.Exceptions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface IZipDownloadPathProvider
    {
        Task<(string ArchiveFilePath, string ArchiveFileKey)> GetFileForArchivingAsync(string filePath, CancellationToken cancellationToken = default);
        Task<string> ParseArchiveFileKeyAsync(string filePath, string archiveFileKey, CancellationToken cancellationToken = default);
    }

    public class TempZipDownloadPathProvider : IZipDownloadPathProvider
    {
        private static readonly string Prefix = nameof(elFinder);

        public TempZipDownloadPathProvider()
        {
        }

        public Task<(string ArchiveFilePath, string ArchiveFileKey)> GetFileForArchivingAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tempFileName = $"{Prefix}_{Guid.NewGuid()}_{DateTimeOffset.UtcNow.Ticks}";
            var tempFile = Path.Combine(filePath, tempFileName);
            return Task.FromResult((tempFile, tempFileName));
        }

        public Task<string> ParseArchiveFileKeyAsync(string filePath, string archiveFileKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Path.IsPathRooted(archiveFileKey)) throw new PermissionDeniedException("Malformed key");

            var fullPath = Path.GetFullPath(Path.Combine(filePath, archiveFileKey));
            if (!fullPath.StartsWith(filePath.EndsWith($"{Path.DirectorySeparatorChar}")
                ? filePath : filePath + Path.DirectorySeparatorChar))
                throw new PermissionDeniedException("Malformed key");

            var fileName = Path.GetFileName(fullPath);
            if (!fileName.StartsWith(Prefix))
                throw new PermissionDeniedException("Malformed key");

            return Task.FromResult(fullPath);
        }
    }
}
