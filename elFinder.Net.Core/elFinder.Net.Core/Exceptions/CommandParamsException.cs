using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Core.Exceptions
{
    public class CommandParamsException : ConnectorException
    {
        public CommandParamsException(string cmd)
        {
            Cmd = cmd;
            ErrorResponse = new ErrorResponse(this)
            {
                error = new[] { ErrorResponse.CommandParams, cmd }
            };
        }

        public string Cmd { get; set; }
    }
}
