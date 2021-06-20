using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class ZipArchiveExtensions
    {
        public static async Task AddDirectoryAsync(this ZipArchive zipArchive,
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

                    zipArchive.CreateEntryFromFile(file.FullName, entryName + file.Name);
                }
            }
        }
    }
}
