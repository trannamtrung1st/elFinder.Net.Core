using elFinder.Net.Core;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem;
using elFinder.Net.Drivers.FileSystem.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.AdvancedDemo.Services
{
    public class ApplicationFileSystemDriver : FileSystemDriver
    {
        public ApplicationFileSystemDriver(
            IPathParser pathParser,
            IPictureEditor pictureEditor,
            IVideoEditor videoEditor,
            IZipDownloadPathProvider zipDownloadPathProvider,
            IZipFileArchiver zipFileArchiver,
            IThumbnailBackgroundGenerator thumbnailBackgroundGenerator,
            ICryptographyProvider cryptographyProvider,
            IConnector connector,
            IConnectorManager connectorManager,
            ITempFileCleaner tempFileCleaner)
            : base(pathParser, pictureEditor, videoEditor,
                  zipDownloadPathProvider, zipFileArchiver,
                  thumbnailBackgroundGenerator, cryptographyProvider,
                  connector, connectorManager, tempFileCleaner)
        {
        }

        public async Task<SearchResponse> SearchMatchFolderOnly(SearchCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchResp = new SearchResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cmd.Q,
                        searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
            {
                var hash = item.GetHash(volume, pathParser);
                var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                    item.GetParentHash(volume, pathParser);
                searchResp.files.Add(await item.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
            }

            return searchResp;
        }
    }
}
