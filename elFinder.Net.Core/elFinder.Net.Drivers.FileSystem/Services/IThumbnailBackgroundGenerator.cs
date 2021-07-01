using elFinder.Net.Core;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface IThumbnailBackgroundGenerator
    {
        void TryAddToQueue(IFile file, IFile tmbFile, int size, bool keepAspectRatio, MediaType? mediaType);
    }
}