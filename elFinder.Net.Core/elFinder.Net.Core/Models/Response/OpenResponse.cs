using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Response
{
    public class OpenResponse
    {
        private static readonly string[] _empty = new string[0];
        private static readonly DebugResponse _debug = new DebugResponse();

        public OpenResponse()
        {
            files = new List<object>();
        }

        public OpenResponse(BaseInfoResponse cwd, ConnectorResponseOptions options,
            IVolume volume)
        {
            files = new List<object>();
            this.cwd = cwd;
            this.options = options;
            files.Add(cwd);
            uplMaxFile = volume.MaxUploadFiles;
        }

        public BaseInfoResponse cwd { get; protected set; }
        public DebugResponse debug => _debug;
        public List<object> files { get; protected set; }
        public ConnectorResponseOptions options { get; protected set; }
        public IEnumerable<string> netDrivers => _empty;
        public int? uplMaxFile { get; protected set; }
        public string uplMaxSize => options.uploadMaxSize;
    }
}
