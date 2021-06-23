using System.IO;
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
        public Task<(string ArchiveFilePath, string ArchiveFileKey)> GetFileForArchivingAsync()
        {
            var tempFile = Path.GetTempFileName();
            var tempFileName = Path.GetFileName(tempFile);

            return Task.FromResult((tempFile, tempFileName));
        }

        public Task<string> ParseArchiveFileKeyAsync(string archiveFileKey)
        {
            var tempDirPath = Path.GetTempPath();

            return Task.FromResult(Path.Combine(tempDirPath, archiveFileKey));
        }
    }
}
