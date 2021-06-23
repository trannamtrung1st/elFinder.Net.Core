using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class ParentsResponse
    {
        public ParentsResponse()
        {
            tree = new List<object>();
        }

        public List<object> tree { get; protected set; }
    }
}
