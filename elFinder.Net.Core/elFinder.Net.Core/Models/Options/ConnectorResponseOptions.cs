using elFinder.Net.Core.Models.Command;
using System.Collections.Generic;
using System.Net.Mime;

namespace elFinder.Net.Core.Models.Options
{
    public class ConnectorResponseOptions
    {
        public ConnectorResponseOptions(IDirectory directory, IEnumerable<string> disabled = null, char separator = default)
        {
            var volume = directory.Volume;
            this.disabled = disabled ?? ConnectorCommand.NotSupportedUICommands;
            this.separator = separator == default ? volume.DirectorySeparatorChar : separator;

            path = directory.Volume.Name;
            var relativePath = volume.GetRelativePath(directory);
            if (relativePath != string.Empty)
            {
                path += this.separator + relativePath.Replace(volume.DirectorySeparatorChar, this.separator);
            }

            url = volume.Url ?? string.Empty;
            tmbUrl = volume.ThumbnailUrl ?? string.Empty;
            uploadMaxConn = volume.MaxUploadConnections;
            copyOverwrite = (byte)(volume.CopyOverwrite ? 1 : 0);

            if (volume.MaxUploadSize.HasValue)
            {
                uploadMaxSize = $"{volume.MaxUploadSizeInMb.Value}M";
            }

            var zipMime = MediaTypeNames.Application.Zip;
            archivers = new ArchiveOptions
            {
                create = new[] { zipMime },
                extract = new[] { zipMime },
                createext = new Dictionary<string, string>
                {
                    { zipMime , FileExtensions.Zip }
                }
            };
        }

        public ArchiveOptions archivers { get; set; }

        public IEnumerable<string> disabled { get; set; }

        public byte copyOverwrite { get; set; }

        public string path { get; set; }

        public char separator { get; set; }

        public string tmbUrl { get; set; }

        public string trashHash { get; set; } = string.Empty;

        public int uploadMaxConn { get; set; }

        public string uploadMaxSize { get; set; }

        public string url { get; set; }
    }
}
