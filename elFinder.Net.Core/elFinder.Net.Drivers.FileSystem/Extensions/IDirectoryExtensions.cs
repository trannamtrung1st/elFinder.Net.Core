using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class IDirectoryExtensions
    {
        public static async Task<string> GetCopyNameAsync(this IDirectory directory, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = directory.Name;

            if (string.IsNullOrEmpty(suffix))
            {
                suffix = " - copy";
            }

            string newName = $"{name}{suffix}";
            if (!Directory.Exists(PathHelper.SafelyCombine(directory.Parent.FullName, directory.Parent.FullName, newName)))
                return newName;
            else
            {
                var search = $"{name}*";
                var sameNames = (await directory.Parent.GetDirectoriesAsync(search, false, _ => true, cancellationToken: cancellationToken))
                    .Select(f => f.Name).ToArray();

                var count = 1;
                while (count != int.MaxValue)
                {
                    newName = $"{name}{suffix}({count++})";
                    if (!sameNames.Contains(newName))
                        return newName;
                }

                throw new StackOverflowException();
            }
        }

        public static bool FileExists(this IDirectory directory)
        {
            return File.Exists(directory.FullName);
        }
    }
}
