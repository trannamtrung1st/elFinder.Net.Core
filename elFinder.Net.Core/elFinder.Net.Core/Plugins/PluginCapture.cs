using System;

namespace elFinder.Net.Core.Plugins
{
    public class PluginCapture
    {
        public Type Type { get; set; }
        public Type ImplType { get; set; }
        public Func<IServiceProvider, object, object> CaptureFunc { get; set; }
    }
}
