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

        Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

        Task OverwriteAsync(Stream stream, CancellationToken cancellationToken = default);

        Task<Stream> OpenWriteAsync(FileMode fileMode = FileMode.Create, CancellationToken cancellationToken = default);

        Task<Stream> CreateAsync(CancellationToken cancellationToken = default);

        Task DeleteAsync(CancellationToken cancellationToken = default);

        Task<IFile> CopyToAsync(string newDest, bool copyOverwrite, CancellationToken cancellationToken = default);

        Task<IFile> SafeCopyToAsync(string newDir, bool copyOverwrite = true, string suffix = null, CancellationToken cancellationToken = default);

        Task<IFile> SafeMoveToAsync(string newDir, bool copyOverwrite = true, string suffix = null, CancellationToken cancellationToken = default);

        Task<ImageWithMimeType> CreateThumbAsync(string originalPath, int tmbSize, IPictureEditor pictureEditor, CancellationToken cancellationToken = default);
    }
}
