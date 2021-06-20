using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class SizeResponse
    {
        public SizeResponse()
        {
        }

        public SizeResponse(DirectorySizeAndCount sizeAndCount)
        {
            size = sizeAndCount.Size;
            dirCnt = sizeAndCount.DirectoryCount;
            fileCnt = sizeAndCount.FileCount;
        }

        public long size { get; set; }
        public int fileCnt { get; set; }
        public int dirCnt { get; set; }
        public Dictionary<string, SizeResponse> sizes { get; set; }
    }
}
