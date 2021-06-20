using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class DuplicateResponse
    {
        public DuplicateResponse()
        {
            added = new List<object>();
        }

        public List<object> added { get; set; }
    }
}
