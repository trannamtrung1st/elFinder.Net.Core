using elFinder.Net.Core;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions
{
    public static class PluginManagerExtensions
    {
        public static QuotaOptions GetQuotaOptions(this PluginManager pluginManager)
        {
            if (pluginManager.Features.TryGetValue(typeof(QuotaOptions), out var quotaObj))
                return quotaObj as QuotaOptions;

            return null;
        }
    }
}
