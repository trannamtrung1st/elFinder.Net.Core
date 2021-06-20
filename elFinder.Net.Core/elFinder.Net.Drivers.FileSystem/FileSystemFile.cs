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
        private FileInfo _fileInfo;
        private readonly IVolume _volume;

        public FileSystemFile(string filePath, IVolume volume)
        {
            _fileInfo = new FileInfo(filePath);
            _volume = volume;
        }

        public FileSystemFile(FileInfo fileInfo, IVolume volume)
        {
            _fileInfo = fileInfo;
            _volume = volume;
        }

        public FileAttributes Attributes
        {
            get => _fileInfo.Attributes;
            set => _fileInfo.Attributes = value;
        }

        private ObjectAttribute _objAttr;
        public ObjectAttribute ObjectAttribute
        {
            get
            {
                if (ByPassObjectAttribute) return ObjectAttribute.Default;

                if (!_volume.Own(this)) return ObjectAttribute.Default;

                if (_objAttr == null)
                    _objAttr = this.GetObjectAttribute(_volume);

                return _objAttr;
            }
        }

        public bool ByPassObjectAttribute { get; set; }

        private IDirectory _directory;
        public IDirectory Parent
        {
            get
            {
                if (_directory == null)
                    _directory = new FileSystemDirectory(_fileInfo.Directory, _volume);
                return _directory;
            }
        }

        public string DirectoryName => _fileInfo.DirectoryName;

        public Task<bool> ExistsAsync => Task.FromResult(_fileInfo.Exists);

        public string Extension => _fileInfo.Extension;

        public string FullName => _fileInfo.FullName;

        public Task<DateTime> LastWriteTimeUtcAsync => Task.FromResult(_fileInfo.LastWriteTimeUtc);

        public Task<long> LengthAsync => Task.FromResult(_fileInfo.Length);

        public string Name => _fileInfo.Name;

        private MimeType? _mimeType;
        public MimeType MimeType
        {
            get
            {
                if (_mimeType == null)
                    _mimeType = MimeHelper.GetMimeType(Extension);
                return _mimeType.Value;
            }
        }

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();
            return Task.FromResult<Stream>(_fileInfo.OpenRead());
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _fileInfo.Refresh();
            if (_directory != null) await _directory.RefreshAsync(cancellationToken);
        }

        public async Task WriteAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await this.CanWriteAsync())
                throw new PermissionDeniedException();

            GCHelper.WaitForCollect();
            using (var destination = _fileInfo.OpenWrite())
            {
                await stream.CopyToAsync(destination);
            }
        }

        public async Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await this.CanWriteAsync())
                throw new PermissionDeniedException();

            GCHelper.WaitForCollect();
            return _fileInfo.OpenWrite();
        }

        public Task<Stream> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCreate()) throw new PermissionDeniedException();

            GCHelper.WaitForCollect();

            Stream stream;
            using (stream = _fileInfo.Create()) { }

            _fileInfo.Refresh();

            return Task.FromResult(stream);
        }

        public async Task<ImageWithMimeType> CreateThumbAsync(string originalPath, int tmbSize, IPictureEditor pictureEditor, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await Parent.ExistsAsync)
                await Parent.CreateAsync(cancellationToken);

            using (var original = File.OpenRead(originalPath))
            {
                var thumb = pictureEditor.GenerateThumbnail(original, tmbSize, true);
                await WriteAsync(thumb.ImageStream, cancellationToken);
                return thumb;
            }
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanDelete()) throw new PermissionDeniedException();

            _fileInfo.Delete();
            return Task.CompletedTask;
        }

        public Task<IFile> RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            _fileInfo.MoveTo(newPath);
            return Task.FromResult<IFile>(new FileSystemFile(newPath, _volume));
        }

        public async Task<IFile> CopyToAsync(string newDest, bool copyOverwrite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            var destDriver = await _volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = new FileSystemFile(newDest, destDriver);
            if (!await destInfo.CanCopyToAsync())
                throw new PermissionDeniedException();

            var info = _fileInfo.CopyTo(newDest, copyOverwrite);

            return new FileSystemFile(info, destDriver);
        }

        public async Task<IFile> MoveToAsync(string newDest, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            var destDriver = await _volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = new FileSystemFile(newDest, destDriver);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            _fileInfo.MoveTo(newDest);

            return new FileSystemFile(newDest, destDriver);
        }

        public async Task<IFile> SafeCopyToAsync(string newDir, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, Name);
            var newFile = new FileSystemFile(newPath, _volume);

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

        public async Task<IFile> SafeMoveToAsync(string newDir, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, Name);
            var newFile = new FileSystemFile(newPath, _volume);

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

        public Task<bool> IsChildOfAsync(string fullPath)
        {
            return Task.FromResult(FullName.StartsWith(fullPath + _volume.DirectorySeparatorChar));
        }

        public Task<bool> IsChildOfAsync(IDirectory directory)
        {
            return Task.FromResult(FullName.StartsWith(directory.FullName + _volume.DirectorySeparatorChar));
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
