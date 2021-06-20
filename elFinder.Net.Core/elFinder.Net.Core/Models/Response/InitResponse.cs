using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class InitResponse : OpenResponse
    {
        private static readonly string[] _empty = new string[0];
        public InitResponse(BaseInfoResponse cwd, ConnectorOptions options) : base(cwd, options)
        {
        }

        public string api => ApiValues.Version;
        public IEnumerable<string> netDrivers => _empty;
    }
}
