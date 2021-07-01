using elFinder.Net.Core.Exceptions;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem
{
    public interface IZipDownloadPathProvider
    {
        Task<(string ArchiveFilePath, string ArchiveFileKey)> GetFileForArchivingAsync();
        Task<string> ParseArchiveFileKeyAsync(string archiveFileKey);
    }

    public class TempZipDownloadPathProvider : IZipDownloadPathProvider
    {
        private readonly HMAC _hmac = new HMACSHA256();
        private static readonly string Postfix = '_' + nameof(elFinder);

        public Task<(string ArchiveFilePath, string ArchiveFileKey)> GetFileForArchivingAsync()
        {
            var bytes = Encoding.UTF8.GetBytes(Path.GetTempFileName() + Guid.NewGuid().ToString());
            var tempFile = Path.Combine(Path.GetTempPath(),
                BitConverter.ToString(_hmac.ComputeHash(bytes)).Replace("-", string.Empty) + Postfix);
            var tempFileName = Path.GetFileName(tempFile);
            return Task.FromResult((tempFile, tempFileName));
        }

        public Task<string> ParseArchiveFileKeyAsync(string archiveFileKey)
        {
            var tempDirPath = Path.GetTempPath();

            if (Path.IsPathRooted(archiveFileKey)) throw new PermissionDeniedException("Malformed key");

            var fullPath = Path.GetFullPath(Path.Combine(tempDirPath, archiveFileKey));
            if (!fullPath.StartsWith(tempDirPath.EndsWith($"{Path.DirectorySeparatorChar}")
                ? tempDirPath : (tempDirPath + Path.DirectorySeparatorChar)))
                throw new PermissionDeniedException("Malformed key");

            var fileName = Path.GetFileName(fullPath);
            if (!fileName.EndsWith(Postfix))
                throw new PermissionDeniedException("Malformed key");

            return Task.FromResult(fullPath);
        }
    }
}
