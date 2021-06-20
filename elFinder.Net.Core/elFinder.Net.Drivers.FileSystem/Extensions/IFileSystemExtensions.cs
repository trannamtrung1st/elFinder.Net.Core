using elFinder.Net.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class IFileSystemExtensions
    {
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
