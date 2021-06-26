using elFinder.Net.Core.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Command
{
    public class UploadCommand : TargetCommand
    {
        public UploadCommand()
        {
            Hashes = new Dictionary<string, string>();
        }

        public IEnumerable<IFormFileWrapper> Upload { get; set; }
        public StringValues UploadPath { get; set; }
        public StringValues MTime { get; set; }
        public StringValues Name { get; set; }
        public StringValues Renames { get; set; }
        public string Suffix { get; set; }
        public Dictionary<string, string> Hashes { get; set; }
        public byte? Overwrite { get; set; }

        public IEnumerable<PathInfo> UploadPathInfos { get; set; }
    }
}
