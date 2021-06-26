using System.IO;

namespace elFinder.Net.Drivers.FileSystem.Helpers
{
    public static class PathHelper
    {
        public static string GetFullPath(params string[] paths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(paths));
            return fullPath;
        }

        public static string GetFullPathNormalized(params string[] paths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(paths)).TrimEnd(Path.DirectorySeparatorChar);
            return fullPath;
        }

        public static string NormalizePath(string fullPath)
        {
            return fullPath.TrimEnd(Path.DirectorySeparatorChar);
        }
    }
}
