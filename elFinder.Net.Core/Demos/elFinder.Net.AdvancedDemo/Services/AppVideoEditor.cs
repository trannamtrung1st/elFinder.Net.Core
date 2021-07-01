using elFinder.Net.Core;
using elFinder.Net.Core.Services.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace elFinder.Net.AdvancedDemo.Services
{
    /// <summary>
    /// This class use FFmpeg to generate video thumbnail
    /// </summary>
    public class AppVideoEditor : IVideoEditor
    {
        public const string FFmpegDirectory = nameof(FFmpeg);
        public const string DefaultImageExt = ".png";
        public static readonly IEnumerable<string> VideoExts = new[]
        {
            ".mp4" , ".avi" , ".mxf" , ".webm" , ".mkv" , ".flv", ".mpeg" , ".mov"
        };

        static AppVideoEditor()
        {
            FFmpeg.SetExecutablesPath(Path.Combine(AppContext.BaseDirectory, FFmpegDirectory));
        }

        private readonly IPictureEditor _pictureEditor;

        public AppVideoEditor(IPictureEditor pictureEditor)
        {
            _pictureEditor = pictureEditor;
        }

        public bool CanProcessFile(string fileExtension)
        {
            return VideoExts.Contains(fileExtension, StringComparer.InvariantCultureIgnoreCase);
        }

        public async Task<ImageWithMimeType> GenerateThumbnailAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default)
        {
            string output = Path.GetTempFileName() + DefaultImageExt;
            try
            {
                IConversion conversion = (await FFmpeg.Conversions.FromSnippet.Snapshot(file.FullName, output, TimeSpan.Zero))
                    .SetPreset(ConversionPreset.UltraFast);
                IConversionResult result = await conversion.Start();

                using (var inputImage = new FileStream(output, FileMode.Open))
                {
                    return _pictureEditor.GenerateThumbnail(inputImage, size, keepAspectRatio);
                }
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        public async Task<ImageWithMimeType> GenerateThumbnailInBackgroundAsync(IFile file, int size,
            bool keepAspectRatio, CancellationToken cancellationToken = default)
        {
            string output = Path.GetTempFileName() + DefaultImageExt;
            try
            {
                var mInfo = await FFmpeg.GetMediaInfo(file.FullName);
                IConversion conversion = (await FFmpeg.Conversions.FromSnippet.Snapshot(file.FullName, output, mInfo.Duration / 2))
                    .SetPreset(ConversionPreset.UltraFast);
                IConversionResult result = await conversion.Start();

                using (var inputImage = new FileStream(output, FileMode.Open))
                {
                    return _pictureEditor.GenerateThumbnail(inputImage, size, keepAspectRatio);
                }
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }
    }
}
