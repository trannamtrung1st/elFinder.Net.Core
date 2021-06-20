namespace elFinder.Net.Core.Models.Command
{
    public class ZipdlCommand : TargetsCommand
    {
        public string ReqId { get; set; }
        public byte Download { get; set; }
        public PathInfo CwdPath { get; set; }
        public string ArchiveFileKey { get; set; }
        public string DownloadFileName { get; set; }
        public string MimeType { get; set; }
    }
}
