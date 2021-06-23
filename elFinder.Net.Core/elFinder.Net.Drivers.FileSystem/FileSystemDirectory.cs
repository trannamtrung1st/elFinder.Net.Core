using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Models;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Factories;
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
        protected DirectoryInfo directoryInfo;
        protected readonly IVolume volume;
        protected readonly IFileSystemFileFactory fileFactory;
        protected readonly IFileSystemDirectoryFactory directoryFactory;

        internal FileSystemDirectory(string dirName, IVolume volume,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory)
        {
            directoryInfo = new DirectoryInfo(dirName);
            this.volume = volume;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
        }

        internal FileSystemDirectory(DirectoryInfo directoryInfo, IVolume volume,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory)
        {
            this.directoryInfo = directoryInfo;
            this.volume = volume;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
        }

        public IVolume Volume => volume;

        public virtual FileAttributes Attributes
        {
            get => directoryInfo.Attributes;
            set => directoryInfo.Attributes = value;
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

        public virtual Task<bool> ExistsAsync => Task.FromResult(directoryInfo.Exists);

        public virtual string FullName => directoryInfo.FullName;

        public virtual Task<DateTime> LastWriteTimeUtcAsync => Task.FromResult(directoryInfo.LastWriteTimeUtc);

        public virtual string Name => directoryInfo.Name;

        private IDirectory _parent;
        public virtual IDirectory Parent
        {
            get
            {
                if (_parent == null && directoryInfo.Parent != null)
                    _parent = directoryFactory.Create(directoryInfo.Parent, volume, fileFactory);
                return _parent;
            }
        }

        public virtual Task<bool> IsParentOfAsync(IFileSystem fileSystem, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(fileSystem.FullName.StartsWith(FullName + volume.DirectorySeparatorChar));
        }

        public virtual Task<bool> IsParentOfAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(fullPath.StartsWith(FullName + volume.DirectorySeparatorChar));
        }

        public virtual Task CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCreate()) throw new PermissionDeniedException();

            directoryInfo.Create();
            directoryInfo.Refresh();
            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanDelete()) throw new PermissionDeniedException();

            foreach (var file in await GetFilesAsync(false, cancellationToken))
                await file.DeleteAsync(cancellationToken);

            foreach (var dir in await GetDirectoriesAsync(false, cancellationToken))
                await dir.DeleteAsync(cancellationToken);

            directoryInfo.Delete(true);
        }

        public virtual async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            directoryInfo.Refresh();
            if (_parent != null) await _parent.RefreshAsync(cancellationToken);
        }

        public virtual async Task<bool> HasAnySubDirectoryAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return false;

            return (await GetDirectoriesAsync(visibleOnly, cancellationToken)).Any();
        }

        public virtual Task<IEnumerable<IDirectory>> GetDirectoriesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = directoryInfo.EnumerateDirectories().Select(dir => directoryFactory.Create(dir, volume, fileFactory) as IDirectory);

            if (visibleOnly) dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public virtual Task<IEnumerable<IDirectory>> GetDirectoriesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = directoryInfo.EnumerateDirectories(search, searchOption).Select(dir => directoryFactory.Create(dir, volume, fileFactory) as IDirectory);

            if (visibleOnly) dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles().Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(IEnumerable<string> mimeTypes, bool visibleOnly = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles().Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles(search, searchOption).Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(string search, IEnumerable<string> mimeTypes,
            bool visibleOnly = true, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles(search, searchOption).Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (visibleOnly) files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public virtual Task<IDirectory> RenameAsync(string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            directoryInfo.MoveTo(newPath);
            return Task.FromResult<IDirectory>(directoryFactory.Create(newPath, volume, fileFactory));
        }

        public virtual async Task<IDirectory> MoveToAsync(string newDest, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanMove()) throw new PermissionDeniedException();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = directoryFactory.Create(newDest, destDriver, fileFactory);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

            directoryInfo.MoveTo(newDest);

            return directoryFactory.Create(newDest, volume, fileFactory);
        }

        public virtual async Task MergeAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = directoryFactory.Create(newDest, destDriver, fileFactory);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

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
                    var newDir = directoryFactory.Create(Path.Combine(currentNewDest.FullName, dir.Name), volume, fileFactory);
                    queue.Enqueue((dir, newDir));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    await file.SafeMoveToAsync(currentNewDest.FullName, copyOverwrite, suffix, cancellationToken);
                }
            }
        }

        public virtual async Task<DirectorySizeAndCount> GetSizeAndCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default)
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

        public virtual async Task<IDirectory> CopyToAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.CanCopy()) throw new PermissionDeniedException();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = directoryFactory.Create(newDest, destDriver, fileFactory);
            if (!await destInfo.CanCopyToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

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
                    var newDir = directoryFactory.Create(Path.Combine(currentNewDest.FullName, dir.Name), volume, fileFactory);
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
