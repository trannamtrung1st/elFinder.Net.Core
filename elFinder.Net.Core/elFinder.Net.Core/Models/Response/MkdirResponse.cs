using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class MkdirResponse : MkfileResponse
    {
        public MkdirResponse() : base()
        {
            hashes = new Dictionary<string, string>();
        }

        public Dictionary<string, string> hashes { get; set; }
    }
}
