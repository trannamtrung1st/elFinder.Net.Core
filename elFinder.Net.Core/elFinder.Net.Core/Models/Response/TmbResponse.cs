using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class TmbResponse
    {
        public TmbResponse()
        {
            images = new Dictionary<string, string>();
        }

        public Dictionary<string, string> images { get; }
    }
}
