using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class PutResponse
    {
        public PutResponse()
        {
            changed = new List<object>();
        }

        public List<object> changed { get; set; }
    }
}
