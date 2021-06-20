using System.IO;

namespace elFinder.Net.Core.Models.Response
{
    public class FileResponse
    {
        public string FileDownloadName { get; set; }
        public Stream FileStream { get; set; }
        public string ContentType { get; set; }
        public string ContentDisposition { get; set; }
    }
}
