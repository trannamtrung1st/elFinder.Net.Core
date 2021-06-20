using System;

namespace elFinder.Net.Core.Exceptions
{
    public class CommandParamsException : Exception
    {
        public CommandParamsException(string cmd)
        {
            Cmd = cmd;
        }

        public string Cmd { get; set; }
    }
}
