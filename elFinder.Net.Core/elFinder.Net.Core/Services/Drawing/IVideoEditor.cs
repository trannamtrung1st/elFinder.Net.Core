using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Services.Drawing
{
    public interface IVideoEditor
    {
        bool CanProcessFile(string fileExtension);

        Task<ImageWithMimeType> GenerateThumbnailAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default);

        Task<ImageWithMimeType> GenerateThumbnailInBackgroundAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default);
    }
}
