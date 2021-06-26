using elFinder.Net.Core.Models.FileInfo;
using System.IO.Compression;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class IZipArchiveEntryExtensions
    {
        public static ArchivedFileEntry ToEntry(this ZipArchiveEntry entry)
        {
            return new ArchivedFileEntry
            {
                LastWriteTime = entry.LastWriteTime,
                Name = entry.Name,
                FullName = entry.FullName,
                CompressedLength = entry.CompressedLength,
                Length = entry.Length
            };
        }
    }
}
