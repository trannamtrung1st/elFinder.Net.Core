using elFinder.Net.Core;
using elFinder.Net.Core.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.AspNetCore.Extensions
{
    public static class ConnectorExtensions
    {
        public static async Task<IActionResult> GetPhysicalFileAsync(this ControllerBase controller,
            IConnector connector, string fullPath, CancellationToken cancellationToken = default)
        {
            var ownVolume = connector.Volumes.FirstOrDefault(v => v.Own(fullPath));

            if (ownVolume == null) return controller.Forbid();

            var file = ownVolume.Driver.CreateFile(fullPath, ownVolume);

            if (!await file.ExistsAsync)
                return controller.NotFound();

            if (!file.CanDownload())
                return controller.Forbid();

            return controller.File(await file.OpenReadAsync(cancellationToken: cancellationToken), file.MimeType,
                enableRangeProcessing: true);
        }
    }
}
