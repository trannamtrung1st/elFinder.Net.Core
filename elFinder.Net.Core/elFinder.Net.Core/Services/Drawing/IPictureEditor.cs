using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.IO;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Services.Drawing
{
    public interface IPictureEditor
    {
        Color BackgroundColor { get; set; }

        bool CanProcessFile(string fileExtension);
        Task<ImageWithMimeType> CropAsync(Stream input, int x, int y, int width, int height, int? quality = null);
        Task<ImageWithMimeType> GenerateThumbnailAsync(Stream input, int size, bool keepAspectRatio);
        Size ImageSize(Stream input);
        Size ImageSize(string fullPath);
        Task<ImageWithMimeType> RotateAsync(Image image, IImageFormat format, int angle, Color? background = null, int? quality = null);
        Task<ImageWithMimeType> RotateAsync(Stream input, int angle, string backgroundHex = null, int? quality = null);
        Task<ImageWithMimeType> ScaleAsync(Stream input, int width, int height, int? quality = null);
    }
}