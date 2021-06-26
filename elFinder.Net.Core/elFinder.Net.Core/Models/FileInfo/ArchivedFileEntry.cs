using System;

namespace elFinder.Net.Core.Models.FileInfo
{
    public class ArchivedFileEntry
    {
        public long CompressedLength { get; set; }
        public string FullName { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }
        public long Length { get; set; }
        public string Name { get; set; }
    }
}
