using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;

namespace elFinder.Net.Core.Models.Response
{
    public class InitResponse : OpenResponse
    {
        public InitResponse() : base() { }

        public InitResponse(BaseInfoResponse cwd, ConnectorResponseOptions options, IVolume volume) : base(cwd, options, volume)
        {
        }

        public string api => ApiValues.Version;
    }
}
