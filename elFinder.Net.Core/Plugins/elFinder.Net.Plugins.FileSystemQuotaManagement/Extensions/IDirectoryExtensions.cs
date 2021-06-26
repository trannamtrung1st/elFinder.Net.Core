using elFinder.Net.Core;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions
{
    public static class IDirectoryExtensions
    {
        public static async Task<long> GetPhysicalStorageUsageAsync(this IDirectory directory, CancellationToken cancellationToken = default)
        {
            var dirSizeAndCount = await directory.GetSizeAndCountAsync(verify: false, _ => true, _ => true, cancellationToken);
            return dirSizeAndCount.Size;
        }
    }
}
