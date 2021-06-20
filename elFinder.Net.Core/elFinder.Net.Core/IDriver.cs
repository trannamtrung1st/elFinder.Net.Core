using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IDriver
    {
        void AddVolume(IVolume volume);
        Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default);
        Task<IVolume> FindOwnVolumeAsync(string fullPath, CancellationToken cancellationToken = default);
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
        Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default);
        IFile CreateFileObject(string path, IVolume volume);
    }
}
