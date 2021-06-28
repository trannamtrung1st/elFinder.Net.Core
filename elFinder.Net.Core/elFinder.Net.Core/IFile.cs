using elFinder.Net.Core.Services.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{

    public interface IFile : IFileSystem<IFile>
    {
        string DirectoryName { get; }
        string Extension { get; }
        Task<long> LengthAsync { get; }
        MimeType MimeType { get; }

        Task<IFile> CopyToAsync(string newDest, IVolume destVolume, bool copyOverwrite, bool verify = true, CancellationToken cancellationToken = default);

        Task<ImageWithMimeType> CreateThumbAsync(string originalPath, int tmbSize, IPictureEditor pictureEditor, bool verify = true, CancellationToken cancellationToken = default);

        Task<Stream> OpenReadAsync(bool verify = true, CancellationToken cancellationToken = default);

        Task<Stream> OpenWriteAsync(bool verify = true, FileMode fileMode = FileMode.Create, CancellationToken cancellationToken = default);

        Task OverwriteAsync(Stream stream, bool verify = true, CancellationToken cancellationToken = default);
    }
}
