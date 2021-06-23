using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class UnknownCommandException : ConnectorException
    {
        public UnknownCommandException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.UnknownCommand
            };
        }
    }
}
