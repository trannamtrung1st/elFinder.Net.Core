using elFinder.Net.Core.Models.Command;
using System.Collections.Generic;

namespace elFinder.Net.Core
{
    public class ConnectorOptions
    {
        public virtual MimeDetectOption MimeDetect { get; set; } = MimeDetectOption.Internal;
        public virtual IEnumerable<string> EnabledCommands { get; set; } = ConnectorCommand.AllCommands;
        public virtual IEnumerable<string> DisabledUICommands { get; set; } = ConnectorCommand.NotSupportedUICommands;
        public virtual int DefaultErrResponseTimeoutMs { get; set; } = 500;
    }
}
