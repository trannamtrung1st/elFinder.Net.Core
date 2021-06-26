using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace elFinder.Net.Demo31.Controllers
{
    [Route("api/files")]
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
            await SetupConnectorAsync();
            var cmd = ConnectorHelper.ParseCommand(Request);
            var ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
            var conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
            var actionResult = conResult.ToActionResult(HttpContext);
            return actionResult;
        }

        [Route("thumb/{target}")]
        public async Task<IActionResult> Thumb(string target)
        {
            await SetupConnectorAsync();
            var thumb = await _connector.GetThumbAsync(target, HttpContext.RequestAborted);
            var actionResult = ConnectorHelper.GetThumbResult(thumb);
            return actionResult;
        }

        private async Task SetupConnectorAsync()
        {
            // Volumes registration
            for (var i = 0; i < 5; i++)
            {
                var volume = new Volume(_driver,
                    Startup.MapPath($"~/upload/volume-{i}"), $"/upload/volume-{i}", $"/api/files/thumb/")
                {
                    StartDirectory = Startup.MapPath($"~/upload/volume-{i}/start"),
                    Name = $"Volume {i}",
                    ThumbnailDirectory = PathHelper.GetFullPath("./thumb")
                };

                _connector.AddVolume(volume);
                _driver.AddVolume(volume);

                await volume.Driver.SetupVolumeAsync(volume);
            }

            // Events
            _driver.OnBeforeMove += (sender, args) =>
            {
                Console.WriteLine("Move: " + args.FileSystem.FullName);
                Console.WriteLine("To: " + args.NewDest);
            };
        }
    }
}
