using elFinder.Net.Core.Models.Options;

namespace elFinder.Net.Core.Models.FileInfo
{
    public class RootInfoResponse : DirectoryInfoResponse
    {
        public byte isroot { get; set; }
        public ConnectorOptions options { get; set; }
    }
}
