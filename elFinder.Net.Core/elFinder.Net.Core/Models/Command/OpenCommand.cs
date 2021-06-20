using Microsoft.Extensions.Primitives;

namespace elFinder.Net.Core.Models.Command
{
    public class OpenCommand : TargetCommand
    {
        public byte Init { get; set; }
        public byte Tree { get; set; }
        public StringValues Mimes { get; set; }

        public IVolume Volume { get; set; }
    }
}
