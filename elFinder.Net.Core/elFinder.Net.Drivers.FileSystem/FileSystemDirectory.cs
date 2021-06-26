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

        public virtual string FullName => PathHelper.NormalizePath(directoryInfo.FullName);

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

        public virtual Task CreateAsync(bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanCreate()) throw new PermissionDeniedException();

            directoryInfo.Create();
            directoryInfo.Refresh();
            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanDelete()) throw new PermissionDeniedException();

            foreach (var file in await GetFilesAsync(verify: false, _ => true, cancellationToken: cancellationToken))
                await file.DeleteAsync(verify, cancellationToken);

            foreach (var dir in await GetDirectoriesAsync(verify: false, _ => true, cancellationToken: cancellationToken))
                await dir.DeleteAsync(verify, cancellationToken);

            directoryInfo.Delete(true);
        }

        public virtual Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            directoryInfo.Refresh();

            return Task.CompletedTask;
        }

        public virtual async Task<bool> HasAnySubDirectoryAsync(bool verify = true,
            Func<IDirectory, bool> filter = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return false;

            return (await GetDirectoriesAsync(verify, filter, cancellationToken)).Any();
        }

        public virtual Task<IEnumerable<IDirectory>> GetDirectoriesAsync(bool verify = true,
            Func<IDirectory, bool> filter = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = directoryInfo.EnumerateDirectories().Select(dir => directoryFactory.Create(dir, volume, fileFactory) as IDirectory);

            if (filter != null) dirs = dirs.Where(filter);
            else dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public virtual Task<IEnumerable<IDirectory>> GetDirectoriesAsync(string search, bool verfify = true,
            Func<IDirectory, bool> filter = null, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verfify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IDirectory>>(new IDirectory[0]);

            var dirs = directoryInfo.EnumerateDirectories(search, searchOption)
                .Select(dir => directoryFactory.Create(dir, volume, fileFactory) as IDirectory);

            if (filter != null) dirs = dirs.Where(filter);
            else dirs = dirs.Where(dir => dir.ObjectAttribute.Visible);

            return Task.FromResult(dirs);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(bool verify = true,
            Func<IFile, bool> filter = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles().Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (filter != null) files = files.Where(filter);
            else files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(IEnumerable<string> mimeTypes, bool verify = true,
            Func<IFile, bool> filter = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles().Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (filter != null) files = files.Where(filter);
            else files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(string search, bool verify = true,
            Func<IFile, bool> filter = null, SearchOption searchOption = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles(search, searchOption).Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (filter != null) files = files.Where(filter);
            else files = files.Where(file => file.ObjectAttribute.Visible);

            return Task.FromResult(files);
        }

        public virtual Task<IEnumerable<IFile>> GetFilesAsync(string search, IEnumerable<string> mimeTypes, bool verify = true,
            Func<IFile, bool> filter = null, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !ObjectAttribute.Read) return Task.FromResult<IEnumerable<IFile>>(new IFile[0]);

            var files = directoryInfo.EnumerateFiles(search, searchOption).Select(f => fileFactory.Create(f, volume, directoryFactory) as IFile);

            if (filter != null) files = files.Where(filter);
            else files = files.Where(file => file.ObjectAttribute.Visible);

            if (mimeTypes?.Any() == true)
            {
                files = files.Where(f => mimeTypes.Contains(f.MimeType) || mimeTypes.Contains(f.MimeType.Type));
            }

            return Task.FromResult(files);
        }

        public virtual Task<IDirectory> RenameAsync(string newName, bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanRename()) throw new PermissionDeniedException();

            var newPath = PathHelper.GetFullPath(Parent.FullName, newName);
            directoryInfo.MoveTo(newPath);
            return Task.FromResult<IDirectory>(directoryFactory.Create(newPath, volume, fileFactory));
        }

        public virtual async Task<IDirectory> MoveToAsync(string newDest, bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (verify && !this.CanMove()) throw new PermissionDeniedException();

            var destDriver = await volume.Driver.FindOwnVolumeAsync(newDest, cancellationToken);
            if (destDriver == null) throw new PermissionDeniedException();

            var destInfo = directoryFactory.Create(newDest, destDriver, fileFactory);
            if (verify && !await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

            directoryInfo.MoveTo(newDest);

            return directoryFactory.Create(newDest, volume, fileFactory);
        }

        public virtual async Task<DirectorySizeAndCount> GetSizeAndCountAsync(bool verify = true, Func<IDirectory, bool> dirFilter = null,
            Func<IFile, bool> fileFilter = null, CancellationToken cancellationToken = default)
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

                foreach (var dir in (await currentDir.GetDirectoriesAsync(verify, dirFilter, cancellationToken: cancellationToken)))
                    queue.Enqueue(dir);

                foreach (var file in (await currentDir.GetFilesAsync(verify, fileFilter, cancellationToken: cancellationToken)))
                {
                    model.FileCount++;
                    model.Size += await file.LengthAsync;
                }
            }

            return model;
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
