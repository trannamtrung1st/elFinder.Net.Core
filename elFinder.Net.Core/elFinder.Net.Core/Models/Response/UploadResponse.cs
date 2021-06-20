using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class UploadResponse
    {
        public IEnumerable<ErrorResponse> _warningDetails;

        public UploadResponse()
        {
            added = new List<object>();
        }

        public List<object> added { get; set; }
        public List<object> warning { get; set; }

        public void SetWarningDetails(IEnumerable<ErrorResponse> errors)
        {
            _warningDetails = errors;
        }

        public IEnumerable<ErrorResponse> GetWarningDetails()
        {
            return _warningDetails;
        }
    }
}
