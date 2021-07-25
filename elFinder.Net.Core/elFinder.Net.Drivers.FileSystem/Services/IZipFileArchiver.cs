using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Drivers.FileSystem.Extensions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface IZipFileArchiver
    {
        Task AddDirectoryAsync(ZipArchive zipArchive, IDirectory dir, string fromDir, bool isDownload, string rootDirReplacement = null, CancellationToken cancellationToken = default);
        void CreateEntryFromFile(ZipArchive zipArchive, IFile file, string entryName);
        Task ExtractToAsync(ZipArchiveEntry entry, IFile dest, bool overwrite, CancellationToken cancellationToken = default);
    }

    public class ZipFileArchiver : IZipFileArchiver
    {
        public void CreateEntryFromFile(ZipArchive zipArchive, IFile file, string entryName)
        {
            if (!file.CanBeArchived()) throw new PermissionDeniedException();

            zipArchive.CreateEntryFromFile(file.FullName, entryName);
        }

        public async Task AddDirectoryAsync(ZipArchive zipArchive,
            IDirectory dir, string fromDir, bool isDownload, string rootDirReplacement = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!dir.CanBeArchived() || (isDownload && !dir.CanDownload()))
                throw new PermissionDeniedException();

            var queue = new Queue<(IDirectory Dir, string FromDir)>();
            queue.Enqueue((dir, fromDir));

            while (queue.Count > 0)
            {
                var (currentDir, currentFromDir) = queue.Dequeue();

                string entryName = Path.Combine(currentFromDir,
                    currentDir == dir ? $"{rootDirReplacement ?? currentDir.Name}/" : $"{currentDir.Name}/");
                zipArchive.CreateEntry(entryName);

                foreach (var subDir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    if (!subDir.CanBeArchived() || (isDownload && !subDir.CanDownload()))
                        throw new PermissionDeniedException();

                    queue.Enqueue((subDir, entryName));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    if (!file.CanBeArchived() || (isDownload && !file.CanDownload()))
                        throw new PermissionDeniedException();

                    CreateEntryFromFile(zipArchive, file, entryName + file.Name);
                }
            }
        }

        public async Task ExtractToAsync(ZipArchiveEntry entry, IFile dest, bool overwrite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await dest.CanExtractToAsync(cancellationToken: cancellationToken)) throw new PermissionDeniedException();

            if (dest.DirectoryExists())
                throw new ExistsException(dest.Name);

            entry.ExtractToFile(dest.FullName, overwrite);
        }
    }
}
