namespace elFinder.Net.Core.Models.FileInfo
{
    public class DirectoryInfoResponse : BaseInfoResponse
    {
        public string volumeid { get; set; }
        public byte dirs { get; set; }
        public string phash { get; set; }
    }
}
