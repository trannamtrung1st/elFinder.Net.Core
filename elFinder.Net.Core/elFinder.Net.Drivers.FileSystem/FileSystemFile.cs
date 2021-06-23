using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Factories;
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
        protected readonly IFileSystemDirectoryFactory directoryFactory;
        protected readonly IFileSystemFileFactory fileFactory;

        internal FileSystemFile(string filePath, IVolume volume,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory)
        {
            fileInfo = new FileInfo(filePath);
            this.volume = volume;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
        }

        internal FileSystemFile(FileInfo fileInfo, IVolume volume,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory)
        {
            this.fileInfo = fileInfo;
            this.volume = volume;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
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

        private IDirectory _directory;
        public virtual IDirectory Parent
        {
            get
            {
                if (_directory == null)
                    _directory = directoryFactory.Create(fileInfo.Directory, volume, fileFactory);
                return _directory;
            }
        }

        public virtual string DirectoryName => fileInfo.DirectoryName;

        public virtual Task<bool> ExistsAsync => Task.FromResult(fileInfo.Exists);

        public virtual string Extension => fileInfo.Extension;

        public virtual string FullName => fileInfo.FullName;

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

        public virtual Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();
            return Task.FromResult<Stream>(fileInfo.OpenRead());
        }

        public virtual async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            fileInfo.Refresh();
            if (_directory != null) await _directory.RefreshAsync(cancellationToken);
        }

        public virtual async Task OverwriteAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await this.CanWriteAsync())
                throw new PermissionDeniedException();

            if (this.DirectoryExists())
                throw new ExistsException(Name);

            GCHelper.WaitForCollect();
            using (var destination = File.Open(fileInfo.FullName, FileMode.Create))
            {
                await stream.CopyToAsync(destination);
            }
        }

        public virtual async Task<Stream> OpenWriteAsync(FileMode fileMode = FileMode.Create, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await this.CanWriteAsync())
                throw new PermissionDeniedException();

            if (this.DirectoryExists())
                throw new ExistsException(Name);

            GCHelper.WaitForCollect();
            return File.Open(fileInfo.FullName, fileMode);
        }

        public virtual Task<Stream> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCreate()) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();

            Stream stream;
            using (stream = fileInfo.Create()) { }

            fileInfo.Refresh();

            return Task.FromResult(stream);
        }

        public virtual async Task<ImageWithMimeType> CreateThumbAsync(string originalPath, int tmbSize, IPictureEditor pictureEditor, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await Parent.ExistsAsync)
                await Parent.CreateAsync(cancellationToken);

            using (var original = File.OpenRead(originalPath))
            {
                var thumb = pictureEditor.GenerateThumbnail(original, tmbSize, true);
                await OverwriteAsync(thumb.ImageStream, cancellationToken);
                return thumb;
            }
        }

        public virtual Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanDelete()) throw new PermissionDeniedException();

            fileInfo.Delete();
            return Task.CompletedTask;
        }

        public virtual Task<IFile> RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            fileInfo.MoveTo(newPath);
            return Task.FromResult<IFile>(fileFactory.Create(newPath, volume, directoryFactory));
        }

        public virtual async Task<IFile> CopyToAsync(string newDest, bool copyOverwrite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = fileFactory.Create(newDest, destDriver, directoryFactory);
            if (!await destInfo.CanCopyToAsync())
                throw new PermissionDeniedException();

            if (destInfo.DirectoryExists())
                throw new ExistsException(destInfo.Name);

            var info = fileInfo.CopyTo(newDest, copyOverwrite);

            return fileFactory.Create(info, destDriver, directoryFactory);
        }

        public virtual async Task<IFile> MoveToAsync(string newDest, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = fileFactory.Create(newDest, destDriver, directoryFactory);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.DirectoryExists())
                throw new ExistsException(destInfo.Name);

            fileInfo.MoveTo(newDest);

            return fileFactory.Create(newDest, destDriver, directoryFactory);
        }

        public virtual async Task<IFile> SafeCopyToAsync(string newDir, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, Name);
            var newFile = fileFactory.Create(newPath, volume, directoryFactory);

            if (File.Exists(newPath))
            {
                if (!copyOverwrite)
                {
                    var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken);
                    newPath = Path.Combine(newDir, newName);
                }
            }

            return await CopyToAsync(newPath, copyOverwrite, cancellationToken);
        }

        public virtual async Task<IFile> SafeMoveToAsync(string newDir, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, Name);
            var newFile = fileFactory.Create(newPath, volume, directoryFactory);

            if (await newFile.ExistsAsync)
            {
                if (!copyOverwrite)
                {
                    var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken);
                    newPath = Path.Combine(newDir, newName);
                }
                else
                {
                    await CopyToAsync(newPath, true, cancellationToken);
                    await DeleteAsync(cancellationToken);
                    await newFile.RefreshAsync(cancellationToken);
                    return newFile;
                }
            }

            return await MoveToAsync(newPath, cancellationToken);
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
