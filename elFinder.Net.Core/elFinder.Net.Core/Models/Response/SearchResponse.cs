using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class SearchResponse
    {
        public SearchResponse()
        {
            files = new List<object>();
        }

        public List<object> files { get; set; }

        public void Concat(SearchResponse another)
        {
            files.AddRange(another.files);
        }
    }
}
