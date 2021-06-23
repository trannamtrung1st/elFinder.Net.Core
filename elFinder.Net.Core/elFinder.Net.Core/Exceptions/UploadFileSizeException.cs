using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class UploadFileSizeException : ConnectorException
    {
        public UploadFileSizeException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.UploadFileSize
            };
        }
    }
}
