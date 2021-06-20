using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class ArchiveResponse
    {
        public ArchiveResponse()
        {
            added = new List<object>();
        }

        public List<object> added { get; set; }
    }
}
