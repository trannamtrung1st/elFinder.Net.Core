using elFinder.Net.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IDirectory : IFileSystem<IDirectory>
    {
        Task<bool> IsParentOfAsync(IFileSystem fileSystem, CancellationToken cancellationToken = default);

        Task<bool> IsParentOfAsync(string fullPath, CancellationToken cancellationToken = default);

        Task<bool> HasAnySubDirectoryAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);

        Task CreateAsync(CancellationToken cancellationToken = default);

        Task DeleteAsync(CancellationToken cancellationToken = default);

        Task MergeAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default);

        Task<IDirectory> CopyToAsync(string newDest, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default);

        Task<IEnumerable<IDirectory>> GetDirectoriesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);

        Task<IEnumerable<IDirectory>> GetDirectoriesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(IEnumerable<string> mimeTypes, bool visibleOnly = true, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(string search, IEnumerable<string> mimeTypes, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default);

        Task<IEnumerable<IFile>> GetFilesAsync(string search, bool visibleOnly = true, SearchOption searchOption = default, CancellationToken cancellationToken = default);

        Task<DirectorySizeAndCount> GetSizeAndCountAsync(bool visibleOnly = true, CancellationToken cancellationToken = default);
    }
}
