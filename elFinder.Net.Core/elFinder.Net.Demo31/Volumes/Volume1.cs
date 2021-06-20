using elFinder.Net.Core;

namespace elFinder.Net.Demo31.Volumes
{
    public interface IVolume1 : IVolume
    {
    }

    public class Volume1 : Volume, IVolume1
    {
        public Volume1(IDriver driver, string rootDirectory, string url, string thumbUrl, char directorySeparatorChar = '\0') : base(driver, rootDirectory, url, thumbUrl, directorySeparatorChar)
        {
        }
    }
}
