using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class InfoResponse
    {
        public InfoResponse()
        {
            files = new List<object>();
        }

        public List<object> files { get; protected set; }
    }
}
