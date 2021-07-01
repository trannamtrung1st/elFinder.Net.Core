using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Services.Drawing
{
    public class DefaultVideoEditor : IVideoEditor
    {
        public bool CanProcessFile(string fileExtension)
        {
            return false;
        }

        public Task<ImageWithMimeType> GenerateThumbnailAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(ImageWithMimeType));
        }

        public Task<ImageWithMimeType> GenerateThumbnailInBackgroundAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(ImageWithMimeType));
        }
    }
}
