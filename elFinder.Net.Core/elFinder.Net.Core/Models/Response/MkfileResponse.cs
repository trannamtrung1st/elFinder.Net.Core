using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class MkfileResponse
    {
        public MkfileResponse()
        {
            added = new List<object>();
        }

        public List<object> added { get; set; }
    }
}
