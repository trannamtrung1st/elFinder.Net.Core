using elFinder.Net.Core.Http;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services.Drawing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IDriver
    {
        #region Events
        event EventHandler<IDirectory> OnBeforeMakeDir;
        event EventHandler<IDirectory> OnAfterMakeDir;
        event EventHandler<IFile> OnBeforeMakeFile;
        event EventHandler<IFile> OnAfterMakeFile;
        event EventHandler<(IFileSystem FileSystem, string RenameTo)> OnBeforeRename;
        event EventHandler<(IFileSystem FileSystem, string PrevName)> OnAfterRename;
        event EventHandler<IFileSystem> OnBeforeRemove;
        event EventHandler<IFileSystem> OnAfterRemove;
        event EventHandler<(IFile File, IFormFileWrapper FormFile, bool IsOverwrite)> OnBeforeUpload;
        event EventHandler<(IFile File, IFormFileWrapper FormFile, bool IsOverwrite)> OnAfterUpload;
        event EventHandler<Exception> OnUploadError;
        event EventHandler<(IFileSystem FileSystem, string NewDest, bool IsOverwrite)> OnBeforeMove;
        event EventHandler<(IFileSystem FileSystem, IFileSystem NewFileSystem, bool IsOverwrite)> OnAfterMove;
        event EventHandler<(IFileSystem FileSystem, string Dest, bool IsOverwrite)> OnBeforeCopy;
        event EventHandler<(IFileSystem FileSystem, IFileSystem NewFileSystem, bool IsOverwrite)> OnAfterCopy;
        event EventHandler<IFile> OnBeforeArchive;
        event EventHandler<IFile> OnAfterArchive;
        event EventHandler<(Exception Exception, IFile File)> OnArchiveError;
        event EventHandler<(IDirectory Parent, IDirectory FromDir, IFile ArchivedFile)> OnBeforeExtract;
        event EventHandler<(IDirectory Parent, IDirectory FromDir, IFile ArchivedFile)> OnAfterExtract;
        event EventHandler<(ArchivedFileEntry Entry, IFile DestFile, bool IsOverwrite)> OnBeforeExtractFile;
        event EventHandler<(ArchivedFileEntry Entry, IFile DestFile, bool IsOverwrite)> OnAfterExtractFile;
        event EventHandler<(byte[] Data, IFile File)> OnBeforeWriteData;
        event EventHandler<(byte[] Data, IFile File)> OnAfterWriteData;
        event EventHandler<(Func<Task<Stream>> OpenStreamFunc, IFile File)> OnBeforeWriteStream;
        event EventHandler<(Func<Task<Stream>> OpenStreamFunc, IFile File)> OnAfterWriteStream;
        event EventHandler<(string Content, string Encoding, IFile File)> OnBeforeWriteContent;
        event EventHandler<(string Content, string Encoding, IFile File)> OnAfterWriteContent;
        #endregion

        Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default);
        Task<OpenResponse> OpenAsync(OpenCommand cmd, CancellationToken cancellationToken = default);
        Task<InfoResponse> InfoAsync(InfoCommand cmd, CancellationToken cancellationToken = default);
        Task<ArchiveResponse> ArchiveAsync(ArchiveCommand cmd, CancellationToken cancellationToken = default);
        Task<ExtractResponse> ExtractAsync(ExtractCommand cmd, CancellationToken cancellationToken = default);
        Task<FileResponse> FileAsync(FileCommand cmd, CancellationToken cancellationToken = default);
        Task<MkdirResponse> MkdirAsync(MkdirCommand cmd, CancellationToken cancellationToken = default);
        Task<MkfileResponse> MkfileAsync(MkfileCommand cmd, CancellationToken cancellationToken = default);
        Task<ParentsResponse> ParentsAsync(ParentsCommand cmd, CancellationToken cancellationToken = default);
        Task<PasteResponse> PasteAsync(PasteCommand cmd, CancellationToken cancellationToken = default);
        Task<DuplicateResponse> DuplicateAsync(DuplicateCommand cmd, CancellationToken cancellationToken = default);
        Task<TmbResponse> TmbAsync(TmbCommand cmd, CancellationToken cancellationToken = default);
        Task<DimResponse> DimAsync(DimCommand cmd, CancellationToken cancellationToken = default);
        Task<GetResponse> GetAsync(GetCommand cmd, CancellationToken cancellationToken = default);
        Task<RmResponse> RmAsync(RmCommand cmd, CancellationToken cancellationToken = default);
        Task<LsResponse> LsAsync(LsCommand cmd, CancellationToken cancellationToken = default);
        Task<PutResponse> PutAsync(PutCommand cmd, CancellationToken cancellationToken = default);
        Task<SizeResponse> SizeAsync(SizeCommand cmd, CancellationToken cancellationToken = default);
        Task<RenameResponse> RenameAsync(RenameCommand cmd, CancellationToken cancellationToken = default);
        Task<TreeResponse> TreeAsync(TreeCommand cmd, CancellationToken cancellationToken = default);
        Task<SearchResponse> SearchAsync(SearchCommand cmd, CancellationToken cancellationToken = default);
        Task<UploadResponse> UploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default);
        Task<ResizeResponse> ResizeAsync(ResizeCommand cmd, CancellationToken cancellationToken = default);
        Task<Zipdl1stResponse> ZipdlAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default);
        Task<FileResponse> ZipdlRawAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default);
        Task<ImageWithMimeType> GetThumbAsync(PathInfo target, CancellationToken cancellationToken = default);
        Task<string> GenerateThumbPathAsync(IFile file, CancellationToken cancellationToken = default);
        Task<string> GenerateThumbPathAsync(IDirectory directory, CancellationToken cancellationToken = default);
        Task<(ImageWithMimeType Thumb, IFile ThumbFile, MediaType? MediaType)> CreateThumbAsync(IFile file,
            bool verify = true, CancellationToken cancellationToken = default);
        Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default);
        IFile CreateFile(string fullPath, IVolume volume);
        IDirectory CreateDirectory(string fullPath, IVolume volume);
    }
}
