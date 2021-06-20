using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class ExtractResponse
    {
        public ExtractResponse()
        {
            added = new List<object>();
        }

        public List<object> added { get; set; }
    }
}
