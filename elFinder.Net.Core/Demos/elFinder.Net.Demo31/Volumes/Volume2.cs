using elFinder.Net.Core;

namespace elFinder.Net.Demo31.Volumes
{
    public interface IVolume2 : IVolume
    {
    }

    public class Volume2 : Volume, IVolume2
    {
        public Volume2(IDriver driver, string rootDirectory, string url, string thumbUrl, char directorySeparatorChar = '\0') : base(driver, rootDirectory, url, thumbUrl, directorySeparatorChar)
        {
        }
    }
}
