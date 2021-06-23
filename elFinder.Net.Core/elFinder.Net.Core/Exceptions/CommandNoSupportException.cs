using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class CommandNoSupportException : ConnectorException
    {
        public CommandNoSupportException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.CommandNoSupport
            };
        }
    }
}
