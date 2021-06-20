namespace elFinder.Net.Core.Models.FileInfo
{
    public class BaseInfoResponse
    {
        public static class MimeType
        {
            public const string Directory = "directory";
            public const string File = "file";
        }

        public string name { get; set; }
        public string hash { get; set; }
        public string mime { get; set; }
        public long ts { get; set; }
        public long size { get; set; }
        public byte read { get; set; }
        public byte write { get; set; }
        public byte locked { get; set; }
        //public bool isowner { get; set; }
        //public string alias { get; set; }
        //public string thash { get; set; }
        //public string csscls { get; set; }
        //public string netkey { get; set; }
    }
}
