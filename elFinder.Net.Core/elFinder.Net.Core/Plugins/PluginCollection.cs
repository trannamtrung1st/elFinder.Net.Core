using System.Collections.Generic;

namespace elFinder.Net.Core.Plugins
{
    public class PluginCollection
    {
        public PluginCollection()
        {
            Captures = new List<PluginCapture>();
        }

        public List<PluginCapture> Captures { get; }
    }
}
