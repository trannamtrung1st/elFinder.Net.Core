using System;

namespace elFinder.Net.Core.Models.Response
{
    public class GetResponse
    {
        private Exception _exception;

        public object content { get; set; }
        public string encoding { get; set; }
        public string doconv { get; set; }

        public Exception GetException()
        {
            return _exception;
        }

        public void SetException(Exception ex)
        {
            _exception = ex;
        }
    }
}
