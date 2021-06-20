using Microsoft.Extensions.Primitives;

namespace elFinder.Net.Core.Models.Command
{
    public class MkdirCommand : MkfileCommand
    {
        public StringValues Dirs { get; set; }
    }
}
