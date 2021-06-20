using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class RmResponse
    {
        public RmResponse()
        {
            removed = new List<string>();
        }

        public List<string> removed { get; }
    }
}
