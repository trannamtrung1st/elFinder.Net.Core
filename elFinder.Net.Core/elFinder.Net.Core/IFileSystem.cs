using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IFileSystem
    {
        IVolume Volume { get; }

        IDirectory Parent { get; }

        FileAttributes Attributes { get; set; }

        Task<bool> IsChildOfAsync(string fullPath);

        Task<bool> IsChildOfAsync(IDirectory directory);

        Task<bool> ExistsAsync { get; }

        string FullName { get; }

        Task<DateTime> LastWriteTimeUtcAsync { get; }

        string Name { get; }

        Task RefreshAsync(CancellationToken cancellationToken = default);

        ObjectAttribute ObjectAttribute { get; }

        bool ByPassObjectAttribute { get; set; }
    }

    public interface IFileSystem<Type> : IFileSystem
    {
        Task<Type> RenameAsync(string newName, CancellationToken cancellationToken = default);

        Task<Type> MoveToAsync(string newDest, CancellationToken cancellationToken = default);
    }
}
