using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.AspNetCore.Helper;
using elFinder.Net.Core;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace elFinder.Net.Demo31.Controllers
{
    [Route("api/files")]
    public class FilesController : Controller
    {
        private readonly IConnector _connector;
        private readonly IEnumerable<IVolume> _volumes;

        public FilesController(IConnector connector,
            IEnumerable<IVolume> volumes)
        {
            _connector = connector;
            _volumes = volumes;
        }

        [Route("connector")]
        public async Task<IActionResult> Connector()
        {
            SetupConnector();
            var cmd = ConnectorHelper.ParseCommand(Request);
            var ccTokenSource = ConnectorHelper.RegisterCcTokenSource(HttpContext);
            var conResult = await _connector.ProcessAsync(cmd, ccTokenSource);
            var actionResult = conResult.ToActionResult(HttpContext);
            return actionResult;
        }

        [Route("thumb/{target}")]
        public async Task<IActionResult> Thumb(string target)
        {
            SetupConnector();
            var thumb = await _connector.GetThumbAsync(target, HttpContext.RequestAborted);
            var actionResult = ConnectorHelper.GetThumbResult(thumb);
            return actionResult;
        }

        private void SetupConnector()
        {
            foreach (var volume in _volumes)
            {
                var limitedFolder = $"{volume.RootDirectory}{volume.DirectorySeparatorChar}limited";
                var haloFile = $"{volume.RootDirectory}{volume.DirectorySeparatorChar}halo.txt";
                var adminArea = $"{volume.RootDirectory}{volume.DirectorySeparatorChar}admin-area";

                // The last matched rule will override all previous (if its flags are defined)
                volume.ObjectAttributes = new List<FilteredObjectAttribute>()
                {
                    // You can implement your own logic to modify Physical File attributes to maintain the Access Control attributes
                    // even if the files are moved
                    new FilteredObjectAttribute()
                    {
                        Expression = $"file.attributes.readonly", // Example only
                        ObjectFilter = (obj) => (int)obj.Attributes != -1 && obj.Attributes.HasFlag(FileAttributes.ReadOnly),
                        Write = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"file.attributes.hidden", // Example only
                        ObjectFilter = (obj) => (int)obj.Attributes != -1 && obj.Attributes.HasFlag(FileAttributes.Hidden),
                        Visible = false
                    },

                    // Recommended: If the parent is limited, then its children should be too.
                    new FilteredObjectAttribute()
                    {
                        Expression = $"dir.fullname = '{adminArea}'//children", // Example only
                        DirectoryFilter = (dir) => dir.FullName == adminArea,
                        ObjectFilter = (obj) => obj.IsChildOfAsync(adminArea).Result,
                        Access = false
                    },

                    // More examples
                    new FilteredObjectAttribute()
                    {
                        Expression = $"obj.fullname = '{limitedFolder}'", // Example only
                        ObjectFilter = (obj) => obj.FullName == limitedFolder,
                        Locked = true, Read = false, Write = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"obj.fullname = '{limitedFolder}'", // Example only
                        ObjectFilter = (obj) => obj.FullName == limitedFolder,
                        Locked = true, Read = false, Write = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"obj.fullname = '{haloFile}'", // Example only
                        ObjectFilter = (obj) => obj.FullName == haloFile,
                        Locked = true, Write = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"file.name.startsWith = 'secrets_'", // Example only
                        FileFilter = (file) => file.Name.StartsWith("secrets_"),
                        Locked = true, Write = false, Visible = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"file.mime = 'somemime-type'", // Example only
                        FileFilter = (file) => file.MimeType == "somemime-type",
                        Locked = true, Write = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"file.ext = 'exe'", // Example only
                        FileFilter = (file) => file.Extension == ".exe",
                        Locked = true, Write = false, ShowOnly = true, Read = false
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"dir.parent.name = 'locked'", // Example only
                        DirectoryFilter = (dir) => dir.Parent.Name == "locked",
                        Locked = true
                    },
                    new FilteredObjectAttribute()
                    {
                        Expression = $"dir.name = 'access-denied'", // Example only
                        DirectoryFilter = (dir) => dir.Name == "access-denied",
                        Access = false
                    },
                    //new FilteredObjectAttribute()
                    //{
                    //    Expression = $"obj.isroot", // Example only
                    //    ObjectFilter = (obj) => volume.IsRoot(obj),
                    //    Access = true, Visible = true, Read = true, Write = true, Locked = false, ShowOnly = false
                    //},
                };

                // Or if you want to restrict all by default
                //volume.DefaultObjectAttribute = new ObjectAttribute
                //{
                //    Visible = false,
                //    Access = false
                //};

                _connector.AddVolume(volume);
                volume.Driver.AddVolume(volume);
            }
        }
    }
}
