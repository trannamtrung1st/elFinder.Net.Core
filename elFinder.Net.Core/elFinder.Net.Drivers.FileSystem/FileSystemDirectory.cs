using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Models;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem
{
    public class FileSystemDirectory : IDirectory
    {
        private DirectoryInfo _directoryInfo;
        private readonly IVolume _volume;

        public FileSystemDirectory(string dirName, IVolume volume)
        {
            _directoryInfo = new DirectoryInfo(dirName);
            _volume = volume;
        }

        public FileSystemDirectory(DirectoryInfo directoryInfo, IVolume volume)
        {
            _directoryInfo = directoryInfo;
            _volume = volume;
        }

        public FileAttributes Attributes
        {
            get => _directoryInfo.Attributes;
            set => _directoryInfo.Attributes = value;
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

        public Task<bool> ExistsAsync => Task.FromResult(_directoryInfo.Exists);

        public string FullName => _directoryInfo.FullName;

        public Task<DateTime> LastWriteTimeUtcAsync => Task.FromResult(_directoryInfo.LastWriteTimeUtc);

        public string Name => _directoryInfo.Name;

        private IDirectory _parent;
        public IDirectory Parent
        {
            get
            {
                if (_parent == null && _directoryInfo.Parent != null)
                    _parent = new FileSystemDirectory(_directoryInfo.Parent, _volume);
                return _parent;
            }
        }

        public Task<bool> IsParentOfAsync(IFileSystem fileSystem, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(fileSystem.FullName.StartsWith(FullName + _volume.DirectorySeparatorChar));
        }

        public Task<bool> IsParentOfAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(fullPath.StartsWith(FullName + _volume.DirectorySeparatorChar));
        }

        public Task CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCreate()) throw new PermissionDeniedException();

            _directoryInfo.Create();
            _directoryInfo.Refresh();
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanDelete()) throw new PermissionDeniedException();

            foreach (var file in await GetFilesAsync(false, cancellationToken))
                await file.DeleteAsync(cancellationToken);

            foreach (var dir in await GetDirectoriesAsync(false, cancellationToken))
                await dir.DeleteAsync(cancellationToken);

            _directoryInfo.Delete(true);
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _directoryInfo.Refresh();
            if (_parent != null) await _parent.RefreshAsync(cancellationToken);
        }

        public async Task<bool> HasAnySubDirectoryAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return false;

            return (await GetDirectoriesAsync(visibleOnly, cancellationToken)).Any();
        }

        public Task<IEnumerable<IDirectory>> GetDirectoriesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = _directoryInfo.EnumerateDirectories().Select(dir => new FileSystemDirectory(dir, _volume) as IDirectory);

            if (visibleOnly) dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public Task<IEnumerable<IDirectory>> GetDirectoriesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = _directoryInfo.EnumerateDirectories(search, searchOption).Select(dir => new FileSystemDirectory(dir, _volume) as IDirectory);

            if (visibleOnly) dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public Task<IEnumerable<IFile>> GetFilesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = _directoryInfo.EnumerateFiles().Select(f => new FileSystemFile(f, _volume) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public Task<IEnumerable<IFile>> GetFilesAsync(IEnumerable<string> mimeTypes, bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = _directoryInfo.EnumerateFiles().Select(f => new FileSystemFile(f, _volume) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public Task<IEnumerable<IFile>> GetFilesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = _directoryInfo.EnumerateFiles(search, searchOption).Select(f => new FileSystemFile(f, _volume) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public Task<IEnumerable<IFile>> GetFilesAsync(string search, IEnumerable<string> mimeTypes,
            bool visibleOnly = true, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = _directoryInfo.EnumerateFiles(search, searchOption).Select(f => new FileSystemFile(f, _volume) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public Task<IDirectory> RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            _directoryInfo.MoveTo(newPath);
            return Task.FromResult<IDirectory>(new FileSystemDirectory(newPath, _volume));
        }

        public async Task<IDirectory> MoveToAsync(string newDest, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            var destDriver = await _volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = new FileSystemDirectory(newDest, destDriver);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            _directoryInfo.MoveTo(newDest);

            return new FileSystemDirectory(newDest, _volume);
        }

        public async Task MergeAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destDriver = await _volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = new FileSystemDirectory(newDest, destDriver);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
            queue.Enqueue((this, destInfo));

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                var currentDir = currentItem.Dir;
                var currentNewDest = currentItem.Dest;

                if (!await currentNewDest.ExistsAsync)
                    await currentNewDest.CreateAsync(cancellationToken);

                foreach (var dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var newDir = new FileSystemDirectory(Path.Combine(currentNewDest.FullName, dir.Name), _volume);
                    queue.Enqueue((dir, newDir));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    await file.SafeMoveToAsync(currentNewDest.FullName, copyOverwrite, suffix, cancellationToken);
                }
            }
        }

        public async Task<DirectorySizeAndCount> GetSizeAndCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = new DirectorySizeAndCount()
            {
                DirectoryCount = -1
            };

            var queue = new Queue<IDirectory>();
            queue.Enqueue(this);

            while (queue.Count > 0)
            {
                var currentDir = queue.Dequeue();
                model.DirectoryCount++;

                foreach (var dir in (await currentDir.GetDirectoriesAsync(visibleOnly, cancellationToken)))
                    queue.Enqueue(dir);

                foreach (var file in (await currentDir.GetFilesAsync(visibleOnly, cancellationToken)))
                {
                    model.FileCount++;
                    model.Size += await file.LengthAsync;
                }
            }

            return model;
        }

        public async Task<IDirectory> CopyToAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            var destDriver = await _volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = new FileSystemDirectory(newDest, destDriver);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
            queue.Enqueue((this, destInfo));

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                var currentDir = currentItem.Dir;
                var currentNewDest = currentItem.Dest;

                if (!await currentNewDest.ExistsAsync)
                    await currentNewDest.CreateAsync(cancellationToken);

                foreach (var dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var newDir = new FileSystemDirectory(Path.Combine(currentNewDest.FullName, dir.Name), _volume);
                    queue.Enqueue((dir, newDir));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    await file.SafeCopyToAsync(currentNewDest.FullName, copyOverwrite, suffix, cancellationToken);
                }
            }

            await destInfo.RefreshAsync(cancellationToken);
            return destInfo;
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
            if (obj is IDirectory dir)
                return dir.FullName == FullName;
            return false;
        }

        public override int GetHashCode()
        {
            return FullName.GetHashCode();
        }
    }
}
