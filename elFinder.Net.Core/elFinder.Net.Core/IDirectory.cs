using elFinder.Net.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IDirectory : IFileSystem<IDirectory>
    {
        Task<IEnumerable<IDirectory>> GetDirectoriesAsync(bool verify = true, Func<IDirectory, bool> filter = null, CancellationToken cancellationToken = default);

        Task<IEnumerable<IDirectory>> GetDirectoriesAsync(string search, bool verfify = true, Func<IDirectory, bool> filter = null, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(bool verify = true, Func<IFile, bool> filter = null, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(IEnumerable<string> mimeTypes, bool verify = true, Func<IFile, bool> filter = null, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(string search, bool verify = true, Func<IFile, bool> filter = null, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(string search, IEnumerable<string> mimeTypes, bool verify = true, Func<IFile, bool> filter = null, SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default);

        Task<DirectorySizeAndCount> GetSizeAndCountAsync(bool verify = true, Func<IDirectory, bool> dirFilter = null,
            Func<IFile, bool> fileFilter = null, CancellationToken cancellationToken = default);

        Task<bool> HasAnySubDirectoryAsync(bool verify = true, Func<IDirectory, bool> filter = null, CancellationToken cancellationToken = default);

        Task<bool> IsParentOfAsync(IFileSystem fileSystem, CancellationToken cancellationToken = default);

        Task<bool> IsParentOfAsync(string fullPath, CancellationToken cancellationToken = default);
    }
}
