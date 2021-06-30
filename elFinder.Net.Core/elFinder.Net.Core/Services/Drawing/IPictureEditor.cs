using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Services.Drawing
{
    /// <summary>
    /// Represents a picture editor
    /// </summary>
    public interface IPictureEditor
    {
        /// <summary>
        /// Get or sets background color, which going be used in rotating operations
        /// </summary>
        Color BackgroundColor { get; set; }

        /// <summary>
        /// Determines whether this picture editor can process the given file extension
        /// </summary>
        /// <param name="fileExtension">The extension of the file</param>
        /// <returns><c>True</c> if can process. Otherwise, <c>false</c></returns>
        bool CanProcessFile(string fileExtension);

        /// <summary>
        /// Convert extension of file to browser's compatible images (png, jpg or gif)
        /// </summary>
        /// <param name="originalImageExtension">Extension of original file</param>
        /// <returns>Browser's compatible extension</returns>
        string ConvertThumbnailExtension(string originalImageExtension);

        /// <summary>
        /// Crop image
        /// </summary>
        /// <param name="input">Input stream of image</param>
        /// <param name="x">The x-coordinate of the upper-left corner of the cropping rectangle</param>
        /// <param name="y">The y-coordinate of the upper-left corner of the cropping rectangle</param>
        /// <param name="width">The width of cropping rectangle</param>
        /// <param name="height">The height of cropping rectangle</param>
        ImageWithMimeType Crop(Stream input, int x, int y, int width, int height, long? quality = null);

        /// <summary>
        /// Generates a thumbnail of the given image
        /// </summary>
        /// <param name="input">Input stream of image</param>
        /// <param name="size">Size in pixels of output thumbnail. Thumbnail is square.</param>
        /// <param name="keepAspectRatio"><c>True</c> if aspect ratio of output thumbnail must equal aspect ratio of input image.</param>
        /// <returns>Generated thumbnail</returns>
        ImageWithMimeType GenerateThumbnail(Stream input, int size, bool keepAspectRatio);

        /// <summary>
        /// Generates a thumbnail of the given video
        /// </summary>
        /// <param name="input">Input stream of video</param>
        /// <param name="size">Size in pixels of output thumbnail. Thumbnail is square.</param>
        /// <param name="keepAspectRatio"><c>True</c> if aspect ratio of output thumbnail must equal aspect ratio of input image.</param>
        /// <returns>Generated thumbnail</returns>
        Task<string> GenerateVideoThumbnailAsync(string filePath);

        /// <summary>
        /// Get Width and Height of the image
        /// </summary>
        /// <param name="input">Input stream of image</param>
        Size ImageSize(Stream input);

        Size ImageSize(string fullPath);

        /// <summary>
        /// Resize image
        /// </summary>
        /// <param name="input">Input stream of image</param>
        /// <param name="width">The desired width of the output image</param>
        /// <param name="height">The desired height of the output image</param>
        ImageWithMimeType Resize(Stream input, int width, int height, long? quality = null);

        /// <summary>
        /// Rotate image
        /// </summary>
        /// <param name="input">Input stream of image</param>
        /// <param name="degrees">Angle of rotation in degrees</param>
        ImageWithMimeType Rotate(Stream input, int degrees, Color? background = null, long? quality = null);

        ImageWithMimeType Rotate(Stream input, int degrees, string backgroundHex = null, long? quality = null);

        ImageWithMimeType ChangeQuality(Image image, ImageFormat imageFormat, long value);
    }
}
