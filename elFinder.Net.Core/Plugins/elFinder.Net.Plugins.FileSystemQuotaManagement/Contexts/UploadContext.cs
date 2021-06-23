using elFinder.Net.Core;
using System.Collections.Concurrent;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts
{
    public class UploadContext
    {
        public UploadContext()
        {
            ProceededDirectories = new ConcurrentDictionary<string, IDirectory>();
        }

        public ConcurrentDictionary<string, IDirectory> ProceededDirectories { get; }
    }
}
