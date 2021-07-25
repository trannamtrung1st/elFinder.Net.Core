using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Command
{
    public class UploadCommand : TargetCommand
    {
        public const string ChunkFail = "chunkfail";

        public UploadCommand()
        {
            Hashes = new Dictionary<string, string>();
        }

        public IEnumerable<IFormFileWrapper> Upload { get; set; }
        public string Mimes { get; set; }
        public StringValues UploadPath { get; set; }
        public StringValues MTime { get; set; }
        public StringValues Name { get; set; }
        public StringValues Renames { get; set; }
        public string Suffix { get; set; }
        public Dictionary<string, string> Hashes { get; set; }
        public byte? Overwrite { get; set; }

        public IEnumerable<PathInfo> UploadPathInfos { get; set; }

        #region Chunked upload
        public string UploadName { get; set; }
        public StringValues Chunk { get; set; }
        public StringValues Cid { get; set; }
        public StringValues Range { get; set; }
        public (string UploadingFileName, int CurrentChunkNo, int TotalChunks) ChunkInfo
        {
            get
            {
                if (Cid.ToString().Length == 0) throw new InvalidOperationException();

                return FileHelper.GetChunkInfo(Chunk);
            }
        }

        public (long StartByte, long ChunkLength, long TotalBytes) RangeInfo
        {
            get
            {
                if (Range.ToString().Length == 0) throw new InvalidOperationException();

                return FileHelper.GetRangeInfo(Range);
            }
        }
        #endregion
    }
}
