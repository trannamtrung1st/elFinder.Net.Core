using elFinder.Net.Core.Services;

namespace elFinder.Net.Core.Extensions
{
    public static class IFileSystemExtensions
    {
        public static string GetParentHash(this IFileSystem fileSystem, IVolume volume, IPathParser pathParser)
        {
            if (volume.IsRoot(fileSystem)) return null;

            string parentPath = volume.IsRoot(fileSystem.Parent) ?
                string.Empty : volume.GetRelativePath(fileSystem.Parent);

            return volume.VolumeId + pathParser.Encode(parentPath);
        }

        public static string GetHash(this IFileSystem fileSystem, IVolume volume, IPathParser pathParser)
        {
            string relativePath = volume.IsRoot(fileSystem) ?
                string.Empty : volume.GetRelativePath(fileSystem);

            return volume.VolumeId + pathParser.Encode(relativePath);
        }
    }
}
