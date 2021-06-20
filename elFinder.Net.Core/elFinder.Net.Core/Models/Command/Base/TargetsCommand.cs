using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Command
{
    public abstract class TargetsCommand
    {
        public StringValues Targets { get; set; }
        public IEnumerable<PathInfo> TargetPaths { get; set; }
    }
}
