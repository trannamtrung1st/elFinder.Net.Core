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
    #region Delegates
    public delegate Task BeforeRemoveThumbAsync(PathInfo pathInfo);
    public delegate Task AfterRemoveThumbAsync(PathInfo pathInfo);
    public delegate Task RemoveThumbErrorAsync(Exception exception);
    public delegate Task BeforeMakeDirAsync(IDirectory directory);
    public delegate Task AfterMakeDirAsync(IDirectory directory);
    public delegate Task BeforeMakeFileAsync(IFile file);
    public delegate Task AfterMakeFileAsync(IFile file);
    public delegate Task BeforeRenameAsync(IFileSystem fileSystem, string renameTo);
    public delegate Task AfterRenameAsync(IFileSystem fileSystem, string prevName);
    public delegate Task BeforeRemoveAsync(IFileSystem file);
    public delegate Task AfterRemoveAsync(IFileSystem file);
    public delegate Task BeforeRollbackChunkAsync(IFileSystem file);
    public delegate Task AfterRollbackChunkAsync(IFileSystem file);
    public delegate Task BeforeUploadAsync(
        IFile file,
        IFile destFile,
        IFormFileWrapper formFile,
        bool isOverwrite,
        bool isChunking);
    public delegate Task AfterUploadAsync(
        IFile file,
        IFile destFile,
        IFormFileWrapper formFile,
        bool isOverwrite,
        bool isChunking);
    public delegate Task BeforeChunkMergedAsync(IFile file, bool isOverwrite);
    public delegate Task AfterChunkMergedAsync(IFile file, bool isOverwrite);
    public delegate Task BeforeChunkTransferAsync(IFile chunkFile, IFile destFile, bool isOverwrite);
    public delegate Task AfterChunkTransferAsync(IFile chunkFile, IFile destFile, bool isOverwrite);
    public delegate Task UploadErrorAsync(Exception exception);
    public delegate Task BeforeMoveAsync(IFileSystem fileSystem, string newDest, bool isOverwrite);
    public delegate Task AfterMoveAsync(IFileSystem fileSystem, IFileSystem newFileSystem, bool isOverwrite);
    public delegate Task BeforeCopyAsync(IFileSystem fileSystem, string dest, bool isOverwrite);
    public delegate Task AfterCopyAsync(IFileSystem fileSystem, IFileSystem newFileSystem, bool isOverwrite);
    public delegate Task BeforeArchiveAsync(IFile file);
    public delegate Task AfterArchiveAsync(IFile file);
    public delegate Task ArchiveErrorAsync(Exception exception, IFile file);
    public delegate Task BeforeExtractAsync(IDirectory parent, IDirectory fromDir, IFile archivedFile);
    public delegate Task AfterExtractAsync(IDirectory parent, IDirectory fromDir, IFile archivedFile);
    public delegate Task BeforeExtractFileAsync(ArchivedFileEntry entry, IFile destFile, bool isOverwrite);
    public delegate Task AfterExtractFileAsync(ArchivedFileEntry entry, IFile destFile, bool isOverwrite);
    public delegate Task BeforeWriteDataAsync(byte[] data, IFile file);
    public delegate Task AfterWriteDataAsync(byte[] data, IFile file);
    public delegate Task BeforeWriteStreamAsync(Func<Task<Stream>> openStreamFunc, IFile file);
    public delegate Task AfterWriteStreamAsync(Func<Task<Stream>> openStreamFunc, IFile file);
    public delegate Task BeforeWriteContentAsync(string content, string encoding, IFile file);
    public delegate Task AfterWriteContentAsync(string content, string encoding, IFile file);
    #endregion

    public interface IDriver
    {
        #region Events
        EventList<BeforeRemoveThumbAsync> OnBeforeRemoveThumb { get; }
        EventList<AfterRemoveThumbAsync> OnAfterRemoveThumb { get; }
        EventList<RemoveThumbErrorAsync> OnRemoveThumbError { get; }
        EventList<BeforeMakeDirAsync> OnBeforeMakeDir { get; }
        EventList<AfterMakeDirAsync> OnAfterMakeDir { get; }
        EventList<BeforeMakeFileAsync> OnBeforeMakeFile { get; }
        EventList<AfterMakeFileAsync> OnAfterMakeFile { get; }
        EventList<BeforeRenameAsync> OnBeforeRename { get; }
        EventList<AfterRenameAsync> OnAfterRename { get; }
        EventList<BeforeRemoveAsync> OnBeforeRemove { get; }
        EventList<AfterRemoveAsync> OnAfterRemove { get; }
        EventList<BeforeRollbackChunkAsync> OnBeforeRollbackChunk { get; }
        EventList<AfterRollbackChunkAsync> OnAfterRollbackChunk { get; }
        EventList<BeforeUploadAsync> OnBeforeUpload { get; }
        EventList<AfterUploadAsync> OnAfterUpload { get; }
        EventList<BeforeChunkMergedAsync> OnBeforeChunkMerged { get; }
        EventList<AfterChunkMergedAsync> OnAfterChunkMerged { get; }
        EventList<BeforeChunkTransferAsync> OnBeforeChunkTransfer { get; }
        EventList<AfterChunkTransferAsync> OnAfterChunkTransfer { get; }
        EventList<UploadErrorAsync> OnUploadError { get; }
        EventList<BeforeMoveAsync> OnBeforeMove { get; }
        EventList<AfterMoveAsync> OnAfterMove { get; }
        EventList<BeforeCopyAsync> OnBeforeCopy { get; }
        EventList<AfterCopyAsync> OnAfterCopy { get; }
        EventList<BeforeArchiveAsync> OnBeforeArchive { get; }
        EventList<AfterArchiveAsync> OnAfterArchive { get; }
        EventList<ArchiveErrorAsync> OnArchiveError { get; }
        EventList<BeforeExtractAsync> OnBeforeExtract { get; }
        EventList<AfterExtractAsync> OnAfterExtract { get; }
        EventList<BeforeExtractFileAsync> OnBeforeExtractFile { get; }
        EventList<AfterExtractFileAsync> OnAfterExtractFile { get; }
        EventList<BeforeWriteDataAsync> OnBeforeWriteData { get; }
        EventList<AfterWriteDataAsync> OnAfterWriteData { get; }
        EventList<BeforeWriteStreamAsync> OnBeforeWriteStream { get; }
        EventList<AfterWriteStreamAsync> OnAfterWriteStream { get; }
        EventList<BeforeWriteContentAsync> OnBeforeWriteContent { get; }
        EventList<AfterWriteContentAsync> OnAfterWriteContent { get; }
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
        Task AbortUploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default);
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
