using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Extensions
{
    public static class IFileExtensions
    {
        private static readonly MD5 _md5 = MD5.Create();

        public static async Task<string> GetFileMd5Async(this IFile file, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = file.Name;
            DateTime modified = await file.LastWriteTimeUtcAsync;

            fileName += modified.ToFileTimeUtc();
            var bytes = Encoding.UTF8.GetBytes(fileName);
            return BitConverter.ToString(_md5.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        public static async Task<BaseInfoResponse> ToFileInfoAsync(this IFile file,
            string parentHash,
            IVolume volume, IPathParser pathParser, IPictureEditor pictureEditor, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file == null) throw new ArgumentNullException(nameof(file));
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            string relativePath = volume.GetRelativePath(file);
            var fileLength = await file.LengthAsync;

            FileInfoResponse response;

            if (volume.ThumbnailUrl != null
                && pictureEditor.CanProcessFile(file.Extension) && fileLength > 0
                && file.ObjectAttribute.Read)
            {
                if(file.Extension == ".mp4" || file.Extension == ".avi" || file.Extension == ".mxf" || file.Extension == ".webm"
                || file.Extension == ".mkv" || file.Extension == ".flv"
                || file.Extension == ".mpeg" || file.Extension == ".mov")
                {
                    var vidResponse = new VideoInfoResponse();
                    response = vidResponse;
                    vidResponse.tmb = await volume.GenerateThumbHashAsync(file, pathParser, pictureEditor, cancellationToken);
                }
                else
                {
                    var imgResponse = new ImageInfoResponse();
                    response = imgResponse;
                    imgResponse.tmb = await volume.GenerateThumbHashAsync(file, pathParser, pictureEditor, cancellationToken);
                }
                
            }
            else response = new FileInfoResponse();

            response.read = file.ObjectAttribute.Read == true ? (byte)1 : (byte)0;
            response.write = file.ObjectAttribute.Write == true ? (byte)1 : (byte)0;
            response.locked = file.ObjectAttribute.Locked == true ? (byte)1 : (byte)0;
            response.name = file.Name;
            response.size = fileLength;
            response.ts = new DateTimeOffset(await file.LastWriteTimeUtcAsync).ToUnixTimeSeconds();
            response.mime = MimeHelper.GetMimeType(file.Extension);
            response.hash = volume.VolumeId + pathParser.Encode(relativePath);
            response.phash = parentHash;
            return response;
        }

        public static ObjectAttribute GetObjectAttribute(this IFile file, IVolume volume)
        {
            var objAttr = new ObjectAttribute(volume.DefaultObjectAttribute);

            if (volume.ObjectAttributes != null)
            {
                var attributeFilters = volume.ObjectAttributes.Where(x =>
                    x.FileFilter?.Invoke(file) == true || x.ObjectFilter?.Invoke(file) == true).ToArray();

                foreach (var attr in attributeFilters)
                {
                    if (attr.Locked != null) objAttr.Locked = attr.Locked.Value;
                    if (attr.Read != null) objAttr.Read = attr.Read.Value;
                    if (attr.Visible != null) objAttr.Visible = attr.Visible.Value;
                    if (attr.Write != null) objAttr.Write = attr.Write.Value;
                    if (attr.ShowOnly != null) objAttr.ShowOnly = attr.ShowOnly.Value;
                    if (attr.Access != null) objAttr.Access = attr.Access.Value;
                }
            }

            objAttr.Write = volume.IsReadOnly ? false : objAttr.Write;
            objAttr.Locked = volume.IsLocked ? true : objAttr.Locked;
            objAttr.ShowOnly = volume.IsShowOnly ? true : objAttr.ShowOnly;

            return objAttr;
        }

        public static bool CanGetThumb(this IFile file)
        {
            return file.ObjectAttribute.Read;
        }
    }
}
