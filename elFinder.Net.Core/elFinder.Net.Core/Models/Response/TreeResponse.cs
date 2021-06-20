using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class TreeResponse
    {
        public TreeResponse()
        {
            tree = new List<object>();
        }

        public List<object> tree { get; set; }
    }
}
