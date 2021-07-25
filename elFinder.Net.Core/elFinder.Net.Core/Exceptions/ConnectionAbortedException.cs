using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class ConnectionAbortedException : ConnectorException
    {
        public ConnectionAbortedException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.ConnectionAborted
            };
        }
    }
}
