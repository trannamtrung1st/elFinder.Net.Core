using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Options
{
    public class ArchiveOptions
    {
        public IEnumerable<string> create { get; set; }

        public IEnumerable<string> extract { get; set; }

        public IDictionary<string, string> createext { get; set; }
    }
}
