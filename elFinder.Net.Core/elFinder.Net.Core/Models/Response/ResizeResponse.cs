using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class ResizeResponse
    {
        public ResizeResponse()
        {
            changed = new List<object>();
        }

        public List<object> changed { get; set; }
    }
}
