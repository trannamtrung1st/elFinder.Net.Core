using elFinder.Net.Core.Services;
using System.Threading.Tasks;

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

        public static bool CanBeArchived(this IFileSystem fileSystem)
        {
            return fileSystem.ObjectAttribute.Access;
        }

        public static bool CanCreate(this IFileSystem fileSystem)
        {
            return fileSystem.Parent?.ObjectAttribute.Write != false;
        }

        public static bool CanDelete(this IFileSystem fileSystem)
        {
            return !fileSystem.ObjectAttribute.Locked && fileSystem.Parent?.ObjectAttribute.Write != false;
        }

        public static bool CanRename(this IFileSystem fileSystem)
        {
            return !fileSystem.ObjectAttribute.Locked && fileSystem.Parent?.ObjectAttribute.Write != false;
        }

        public static bool CanMove(this IFileSystem fileSystem)
        {
            return !fileSystem.ObjectAttribute.Locked;
        }

        public static async Task<bool> CanCopyToAsync(this IFileSystem destination)
        {
            if (await destination.ExistsAsync)
                return destination.ObjectAttribute.Write;

            return destination.Parent?.ObjectAttribute.Write != false;
        }

        public static async Task<bool> CanWriteAsync(this IFileSystem destination)
        {
            if (await destination.ExistsAsync)
                return destination.ObjectAttribute.Write;

            return destination.Parent?.ObjectAttribute.Write != false;
        }

        public static async Task<bool> CanMoveToAsync(this IFileSystem destination)
        {
            if (await destination.ExistsAsync)
                return destination.ObjectAttribute.Write;

            return destination.Parent?.ObjectAttribute.Write != false;
        }

        public static async Task<bool> CanExtractToAsync(this IFileSystem destination)
        {
            if (await destination.ExistsAsync)
                return destination.ObjectAttribute.Write;

            return destination.Parent?.ObjectAttribute.Write != false;
        }

        public static bool CanCopy(this IFileSystem fileSystem)
        {
            return fileSystem.ObjectAttribute.Access;
        }

        public static bool CanDownload(this IFileSystem fileSystem)
        {
            return fileSystem.ObjectAttribute.Read && !fileSystem.ObjectAttribute.ShowOnly;
        }
    }
}
