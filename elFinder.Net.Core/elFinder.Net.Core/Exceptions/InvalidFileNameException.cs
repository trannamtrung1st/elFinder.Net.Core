using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class InvalidFileNameException : ConnectorException
    {
        public InvalidFileNameException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.InvalidFileName
            };
        }
    }
}
