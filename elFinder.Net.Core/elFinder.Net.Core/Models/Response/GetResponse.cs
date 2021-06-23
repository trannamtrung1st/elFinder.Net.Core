using System;

namespace elFinder.Net.Core.Models.Response
{
    public class GetResponse
    {
        protected Exception exception;

        public object content { get; set; }
        public string encoding { get; set; }
        public string doconv { get; set; }

        public Exception GetException()
        {
            return exception;
        }

        public void SetException(Exception ex)
        {
            exception = ex;
        }
    }
}
