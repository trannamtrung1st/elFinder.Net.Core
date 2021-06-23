using elFinder.Net.AdvancedDemo.Models;
using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using elFinder.Net.Plugins.FileSystemQuotaManagement;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.AdvancedDemo.Controllers
{
    [Route("api/files")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class FilesController : Controller
    {
        private readonly IConnector _connector;
        private readonly IDriver _driver;

        public FilesController(IConnector connector,
            IDriver driver)
        {
            _connector = connector;
            _driver = driver;
        }

        [Route("connector")]
        public async Task<IActionResult> Connector()
        {
            var ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
            await SetupConnectorAsync(ccTokenSource.Token);
            var cmd = ConnectorHelper.ParseCommand(Request);
            var conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
            var actionResult = conResult.ToActionResult(HttpContext);
            return actionResult;
        }

        [Route("thumb/{target}")]
        public async Task<IActionResult> Thumb(string target)
        {
            await SetupConnectorAsync(HttpContext.RequestAborted);
            var thumb = await _connector.GetThumbAsync(target, HttpContext.RequestAborted);
            var actionResult = ConnectorHelper.GetThumbResult(thumb);
            return actionResult;
        }

        private async Task SetupConnectorAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = User;
            var volumePath = user.FindFirst(nameof(AppUser.VolumePath)).Value;

            var volume = new Volume(_driver, Startup.MapPath($"~/upload/{volumePath}"), $"/upload/{volumePath}/", $"/api/files/thumb/")
            {
                Name = "My volume",
                ThumbnailDirectory = PathHelper.GetFullPath($"./thumb/{volumePath}")
            };

            await _driver.SetupVolumeAsync(volume, cancellationToken);

            _connector.AddVolume(volume);
            _driver.AddVolume(volume);

            #region Quota management
            var quotaOptions = new QuotaOptions() { Enabled = true };
            quotaOptions.Quotas[volume.VolumeId] = new VolumeQuota
            {
                VolumeId = volume.VolumeId,
                MaxStorageSizeInMb = 10
            };
            _connector.PluginManager.Features[typeof(QuotaOptions)] = quotaOptions;
            #endregion
        }
    }
}
