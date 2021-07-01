using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class InvalidDirNameException : ConnectorException
    {
        public InvalidDirNameException()
        {
            ErrorResponse = new ErrorResponse(this)
            {
                error = ErrorResponse.InvalidDirName
            };
        }
    }
}
