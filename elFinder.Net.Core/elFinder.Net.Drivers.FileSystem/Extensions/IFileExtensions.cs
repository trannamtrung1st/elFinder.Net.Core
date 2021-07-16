using elFinder.Net.Core;
using elFinder.Net.Drivers.FileSystem.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Extensions
{
    public static class IFileExtensions
    {
        public static async Task<string> GetCopyNameAsync(this IFile file, string suffix,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = Path.GetFileNameWithoutExtension(file.Name);
            string extension = file.Extension;

            if (string.IsNullOrEmpty(suffix))
            {
                suffix = " - copy";
            }

            string newName = $"{name}{suffix}{extension}";
            if (!File.Exists(PathHelper.SafelyCombine(file.DirectoryName, file.DirectoryName, newName)))
                return newName;
            else
            {
                var search = $"{name}*{extension}";
                var sameNames = (await file.Parent.GetFilesAsync(search, false, _ => true, cancellationToken: cancellationToken))
                    .Select(f => f.Name).ToArray();

                var count = 1;
                while (count != int.MaxValue)
                {
                    newName = $"{name}{suffix}({count++}){extension}";
                    if (!sameNames.Contains(newName))
                        return newName;
                }

                throw new StackOverflowException();
            }
        }

        public static bool DirectoryExists(this IFile file)
        {
            return Directory.Exists(file.FullName);
        }
    }
}
