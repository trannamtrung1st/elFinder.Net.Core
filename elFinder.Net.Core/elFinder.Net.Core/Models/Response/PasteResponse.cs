using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class PasteResponse
    {
        public PasteResponse()
        {
            added = new List<object>();
            removed = new List<string>();
        }

        public List<object> added { get; set; }
        public List<string> removed { get; set; }
    }
}
