using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class OpenResponse
    {
        private static readonly DebugResponse _debug = new DebugResponse();

        public OpenResponse(BaseInfoResponse cwd, ConnectorResponseOptions options)
        {
            files = new List<object>();
            this.cwd = cwd;
            this.options = options;
            files.Add(cwd);
        }

        public BaseInfoResponse cwd { get; protected set; }

        public DebugResponse debug => _debug;

        public List<object> files { get; protected set; }

        public ConnectorResponseOptions options { get; protected set; }
    }
}
