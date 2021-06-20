using System.Collections.Generic;

namespace elFinder.Net.Core.Services
{
    public class PathInfoEqualityComparer : IEqualityComparer<PathInfo>
    {
        public bool Equals(PathInfo x, PathInfo y)
        {
            return x == y;
        }

        public int GetHashCode(PathInfo obj)
        {
            return obj.GetHashCode();
        }
    }
}
