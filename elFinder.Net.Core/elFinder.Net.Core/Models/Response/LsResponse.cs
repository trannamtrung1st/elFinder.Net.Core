using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class LsResponse
    {
        public LsResponse()
        {
            list = new Dictionary<string, string>();
        }

        public Dictionary<string, string> list { get; }
    }
}
