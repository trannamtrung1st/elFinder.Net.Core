using elFinder.Net.Core.Models.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.Threading.Tasks;

namespace elFinder.Net.AspNetCore.Results
{
    public class ElFinderFileResult : FileStreamResult
    {
        public ElFinderFileResult(FileResponse fileResponse) : base(fileResponse.FileStream, fileResponse.ContentType)
        {
            ContentDisposition = fileResponse.ContentDisposition;
            FileDownloadName = fileResponse.FileDownloadName;
        }

        public ElFinderFileResult(Stream fileStream, string contentType) : base(fileStream, contentType)
        {
        }

        public ElFinderFileResult(Stream fileStream, MediaTypeHeaderValue contentType) : base(fileStream, contentType)
        {
        }

        public string ContentDisposition { get; set; }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (ContentDisposition != null)
            {
                var contentDispositionHeader = new ContentDispositionHeaderValue(ContentDisposition);
                contentDispositionHeader.SetHttpFileName(FileDownloadName);
                context.HttpContext.Response.Headers.Add(HeaderNames.ContentDisposition, contentDispositionHeader.ToString());
                FileDownloadName = null;
            }

            return base.ExecuteResultAsync(context);
        }
    }
}
