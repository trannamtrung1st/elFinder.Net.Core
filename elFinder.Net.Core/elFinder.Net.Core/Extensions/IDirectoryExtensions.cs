using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Extensions
{
    public static class IDirectoryExtensions
    {
        public static async Task<BaseInfoResponse> ToFileInfoAsync(this IDirectory directory,
            string hash, string parentHash, IVolume volume,
            ConnectorOptions connectorOptions, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasSubdir = await directory.HasAnySubDirectoryAsync(cancellationToken: cancellationToken);

            if (volume.IsRoot(directory))
            {
                return new RootInfoResponse
                {
                    hash = hash,
                    mime = BaseInfoResponse.MimeType.Directory,
                    dirs = hasSubdir ? (byte)1 : (byte)0,
                    size = 0,
                    ts = new DateTimeOffset(await directory.LastWriteTimeUtcAsync).ToUnixTimeSeconds(),
                    volumeid = volume.VolumeId,
                    phash = parentHash,
                    read = 1,
                    write = volume.IsReadOnly ? (byte)0 : (byte)1,
                    locked = volume.IsLocked ? (byte)1 : (byte)0,
                    name = volume.Name,
                    isroot = 1,
                    options = new ConnectorResponseOptions(directory, connectorOptions.DisabledUICommands, volume.DirectorySeparatorChar)
                };
            }
            else
            {
                return new DirectoryInfoResponse
                {
                    hash = hash,
                    mime = BaseInfoResponse.MimeType.Directory,
                    dirs = hasSubdir ? (byte)1 : (byte)0,
                    size = 0,
                    ts = new DateTimeOffset(await directory.LastWriteTimeUtcAsync).ToUnixTimeSeconds(),
                    volumeid = volume.VolumeId,
                    phash = parentHash,
                    read = directory.ObjectAttribute.Read == true ? (byte)1 : (byte)0,
                    write = directory.ObjectAttribute.Write == true ? (byte)1 : (byte)0,
                    locked = directory.ObjectAttribute.Locked == true ? (byte)1 : (byte)0,
                    name = directory.Name
                };
            }
        }

        public static ObjectAttribute GetObjectAttribute(this IDirectory dir, IVolume volume)
        {
            var objAttr = new ObjectAttribute(volume.DefaultObjectAttribute);

            objAttr.Write = !volume.IsReadOnly;
            objAttr.Locked = volume.IsLocked;
            objAttr.ShowOnly = volume.IsShowOnly;

            if (volume.ObjectAttributes != null)
            {
                var attributeFilters = volume.ObjectAttributes.Where(x =>
                    x.DirectoryFilter?.Invoke(dir) == true || x.ObjectFilter?.Invoke(dir) == true).ToArray();

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

            return objAttr;
        }

        public static bool CanCreateObject(this IDirectory dir)
        {
            return dir.ObjectAttribute.Write;
        }
    }
}
