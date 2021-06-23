using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class UploadResponse
    {
        protected List<ErrorResponse> warningDetails;

        public UploadResponse()
        {
            added = new List<object>();
            _warning = new List<object>();
            warningDetails = new List<ErrorResponse>();
        }

        public List<object> added { get; set; }

        private List<object> _warning;
        public List<object> warning => _warning.Count > 0 ? _warning : null;

        public List<object> GetWarnings()
        {
            return _warning;
        }

        public List<ErrorResponse> GetWarningDetails()
        {
            return warningDetails;
        }
    }
}
