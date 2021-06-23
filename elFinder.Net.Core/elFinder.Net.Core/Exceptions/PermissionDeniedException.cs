using elFinder.Net.Core.Models.Response;
using System.Net;

namespace elFinder.Net.Core.Exceptions
{
    public class PermissionDeniedException : ConnectorException
    {
        public PermissionDeniedException() : base("")
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.PermissionDenied
            };
        }

        public PermissionDeniedException(string message) : base(message)
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.PermissionDenied
            };
        }

        public override HttpStatusCode? StatusCode => HttpStatusCode.Forbidden;
    }
}
