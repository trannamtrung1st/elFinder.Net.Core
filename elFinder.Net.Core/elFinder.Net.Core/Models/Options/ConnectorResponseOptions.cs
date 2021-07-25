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

        public IEnumerable<string> disabled { get; }

        public byte copyOverwrite => 1;

        public string path { get; set; }

        public char separator { get; set; }

        public string tmbUrl { get; set; }

        public string trashHash => string.Empty;

        public int uploadMaxConn { get; set; } = 1;

        public string uploadMaxSize { get; set; }

        public string url { get; set; }
    }
}
