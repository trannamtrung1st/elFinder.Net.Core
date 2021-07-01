using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem
{
    public class FileSystemFile : IFile
    {
        protected FileInfo fileInfo;
        protected readonly IVolume volume;

        public FileSystemFile(string filePath, IVolume volume)
        {
            fileInfo = new FileInfo(filePath);
            this.volume = volume;
        }

        public FileSystemFile(FileInfo fileInfo, IVolume volume)
        {
            this.fileInfo = fileInfo;
            this.volume = volume;
        }

        public IVolume Volume => volume;

        public virtual FileAttributes Attributes
        {
            get => fileInfo.Attributes;
            set => fileInfo.Attributes = value;
        }

        private ObjectAttribute _objAttr;
        public virtual ObjectAttribute ObjectAttribute
        {
            get
            {
                if (ByPassObjectAttribute) return ObjectAttribute.Default;

                if (!volume.Own(this)) return ObjectAttribute.Default;

                if (_objAttr == null)
                    _objAttr = this.GetObjectAttribute(volume);

                return _objAttr;
            }
        }

        public virtual bool ByPassObjectAttribute { get; set; }

        private IDirectory _parent;
        public virtual IDirectory Parent
        {
            get
            {
                if (_parent == null)
                    _parent = new FileSystemDirectory(fileInfo.Directory, volume);
                return _parent;
            }
        }

        public virtual string DirectoryName => fileInfo.DirectoryName;

        public virtual Task<bool> ExistsAsync => Task.FromResult(fileInfo.Exists);

        public virtual string Extension => fileInfo.Extension;

        public virtual string FullName => PathHelper.NormalizePath(fileInfo.FullName);

        public virtual Task<DateTime> LastWriteTimeUtcAsync => Task.FromResult(fileInfo.LastWriteTimeUtc);

        public virtual Task<long> LengthAsync => Task.FromResult(fileInfo.Length);

        public virtual string Name => fileInfo.Name;

        private MimeType? _mimeType;
        public virtual MimeType MimeType
        {
            get
            {
                if (_mimeType == null)
                    _mimeType = MimeHelper.GetMimeType(Extension);
                return _mimeType.Value;
            }
        }

        public virtual Task<Stream> OpenReadAsync(bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();
            return Task.FromResult<Stream>(fileInfo.OpenRead());
        }

        public virtual Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            fileInfo.Refresh();

            return Task.CompletedTask;
        }

        public virtual async Task OverwriteAsync(Stream stream, bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !await this.CanWriteAsync())
                throw new PermissionDeniedException();

            if (this.DirectoryExists())
                throw new ExistsException(Name);

            GCHelper.WaitForCollect();
            using (var destination = File.Open(fileInfo.FullName, FileMode.Create))
            {
                await stream.CopyToAsync(destination);
            }
        }

        public virtual async Task<Stream> OpenWriteAsync(bool verify = true, FileMode fileMode = FileMode.Create, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !await this.CanWriteAsync())
                throw new PermissionDeniedException();

            if (this.DirectoryExists())
                throw new ExistsException(Name);

            GCHelper.WaitForCollect();
            return File.Open(fileInfo.FullName, fileMode);
        }

        public virtual Task CreateAsync(bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanCreate()) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();

            using (var stream = fileInfo.Create()) { }

            fileInfo.Refresh();

            return Task.CompletedTask;
        }

        public virtual async Task<ImageWithMimeType> CreateThumbAsync(IFile file, int tmbSize, IPictureEditor pictureEditor,
            bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await Parent.ExistsAsync)
                await Parent.CreateAsync(verify, cancellationToken: cancellationToken);

            using (var original = await file.OpenReadAsync(verify: false, cancellationToken: cancellationToken))
            {
                var thumb = pictureEditor.GenerateThumbnail(original, tmbSize, true);

                if (thumb == null) return null;

                await OverwriteAsync(thumb.ImageStream, verify, cancellationToken: cancellationToken);

                return thumb;
            }
        }

        public virtual async Task<ImageWithMimeType> CreateThumbAsync(IFile file, int tmbSize, IVideoEditor videoEditor,
            bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await Parent.ExistsAsync)
                await Parent.CreateAsync(verify, cancellationToken: cancellationToken);

            var thumb = await videoEditor.GenerateThumbnailAsync(file, tmbSize, true, cancellationToken: cancellationToken);

            if (thumb == null) return null;

            await OverwriteAsync(thumb.ImageStream, verify, cancellationToken: cancellationToken);

            return thumb;
        }

        public virtual Task DeleteAsync(bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanDelete()) throw new PermissionDeniedException();

            fileInfo.Delete();
            return Task.CompletedTask;
        }

        public virtual Task<IFile> RenameAsync(string newName, bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            fileInfo.MoveTo(newPath);
            return Task.FromResult<IFile>(new FileSystemFile(newPath, volume));
        }

        public virtual async Task<IFile> CopyToAsync(string newDest, IVolume destVolume, bool copyOverwrite,
            bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanCopy()) throw new PermissionDeniedException();

            var destInfo = new FileSystemFile(newDest, destVolume);
            if (verify && !await destInfo.CanCopyToAsync())
                throw new PermissionDeniedException();

            if (destInfo.DirectoryExists())
                throw new ExistsException(destInfo.Name);

            var info = fileInfo.CopyTo(newDest, copyOverwrite);

            return new FileSystemFile(info, destVolume);
        }

        public virtual async Task<IFile> MoveToAsync(string newDest, IVolume destVolume,
            bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanMove()) throw new PermissionDeniedException();

            var destInfo = new FileSystemFile(newDest, destVolume);
            if (verify && !await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.DirectoryExists())
                throw new ExistsException(destInfo.Name);

            fileInfo.MoveTo(newDest);

            return new FileSystemFile(newDest, destVolume);
        }

        public virtual Task<bool> IsChildOfAsync(string fullPath)
        {
            return Task.FromResult(FullName.StartsWith(fullPath + volume.DirectorySeparatorChar));
        }

        public virtual Task<bool> IsChildOfAsync(IDirectory directory)
        {
            return Task.FromResult(FullName.StartsWith(directory.FullName + volume.DirectorySeparatorChar));
        }

        public override bool Equals(object obj)
        {
            if (obj is IFile file)
                return file.FullName == FullName;
            return false;
        }

        public override int GetHashCode()
        {
            return FullName.GetHashCode();
        }
    }
}
