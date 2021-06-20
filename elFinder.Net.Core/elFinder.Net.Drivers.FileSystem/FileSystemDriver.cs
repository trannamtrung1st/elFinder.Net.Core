using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Options;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Helpers;
using elFinder.Net.Drivers.FileSystem.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem
{
    public class FileSystemDriver : IDriver
    {
        private readonly IPathParser _pathParser;
        private readonly IPictureEditor _pictureEditor;
        private readonly IList<IVolume> _volumes;

        public FileSystemDriver(IPathParser pathParser,
            IPictureEditor pictureEditor)
        {
            _pathParser = pathParser;
            _pictureEditor = pictureEditor;
            _volumes = new List<IVolume>();
        }

        public void AddVolume(IVolume volume)
        {
            _volumes.Add(volume);
        }

        public IFile CreateFileObject(string path, IVolume volume)
        {
            return new FileSystemFile(path, volume);
        }

        public async Task<LsResponse> LsAsync(LsCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lsResp = new LsResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetFilesAsync(cmd.Mimes, true, cancellationToken))
            {
                if (cmd.Intersect.Count > 0 && !cmd.Intersect.Contains(item.Name)) continue;

                var hash = item.GetHash(volume, _pathParser);
                lsResp.list[hash] = item.Name;
            }

            foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
            {
                if (cmd.Intersect.Count > 0 && !cmd.Intersect.Contains(item.Name)) continue;

                var hash = item.GetHash(volume, _pathParser);
                lsResp.list[hash] = item.Name;
            }

            return lsResp;
        }

        public async Task<MkdirResponse> MkdirAsync(MkdirCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mkdirResp = new MkdirResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetHash = targetPath.HashedTarget;

            if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

            if (!string.IsNullOrEmpty(cmd.Name))
            {
                var newDir = new FileSystemDirectory(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume);
                await newDir.CreateAsync(cancellationToken);
                var hash = newDir.GetHash(volume, _pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, targetHash, volume, cancellationToken));
            }

            foreach (string dir in cmd.Dirs)
            {
                string dirName = dir.StartsWith("/") ? dir.Substring(1) : dir;
                var newDir = new FileSystemDirectory(Path.Combine(targetPath.Directory.FullName, dirName), volume);
                await newDir.CreateAsync(cancellationToken);

                var hash = newDir.GetHash(volume, _pathParser);
                var parentHash = newDir.GetParentHash(volume, _pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));

                string relativePath = volume.GetRelativePath(newDir);
                mkdirResp.hashes.Add($"/{dirName}", volume.VolumeId + _pathParser.Encode(relativePath));
            }

            return mkdirResp;
        }

        public async Task<MkfileResponse> MkfileAsync(MkfileCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetHash = targetPath.HashedTarget;

            if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

            var newFile = new FileSystemFile(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume);
            await newFile.CreateAsync(cancellationToken);

            var mkfileResp = new MkfileResponse();
            mkfileResp.added.Add(await newFile.ToFileInfoAsync(targetHash, volume, _pathParser, _pictureEditor, cancellationToken));

            return mkfileResp;
        }

        public async Task<OpenResponse> OpenAsync(OpenCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IVolume currentVolume = cmd.Volume;
            IDirectory cwd = null;
            string cwdHash, cwdParentHash;
            OpenResponse openResp;
            PathInfo targetPath = cmd.TargetPath;

            if (cmd.TargetPath?.Directory.ObjectAttribute.Read == false)
                throw new PermissionDeniedException();

            if (cmd.Init == 1)
            {
                if (targetPath == null)
                {
                    if (currentVolume.StartDirectory != null && !currentVolume.IsRoot(currentVolume.StartDirectory)
                        && Directory.Exists(currentVolume.StartDirectory))
                    {
                        cwd = new FileSystemDirectory(currentVolume.StartDirectory, currentVolume);
                    }

                    if (cwd == null || !cwd.ObjectAttribute.Read)
                    {
                        cwd = new FileSystemDirectory(currentVolume.RootDirectory, currentVolume);
                        cwdHash = cwd.GetHash(currentVolume, _pathParser);
                        targetPath = new PathInfo(string.Empty, currentVolume, cwd, cwdHash);
                    }
                    else
                    {
                        cwdHash = cwd.GetHash(currentVolume, _pathParser);
                        targetPath = new PathInfo(currentVolume.GetRelativePath(cwd), currentVolume, cwd, cwdHash);
                    }

                    cmd.TargetPath = targetPath;
                }
                else
                {
                    cwd = targetPath.Directory;
                    cwdHash = targetPath.HashedTarget;
                }

                cwdParentHash = cwd.GetParentHash(currentVolume, _pathParser);
                var initResp = new InitResponse(await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, cancellationToken),
                    new ConnectorOptions(targetPath, '/'));

                if (!targetPath.IsRoot)
                {
                    await AddParentsToListAsync(targetPath, initResp.files, cancellationToken);
                }

                if (currentVolume.MaxUploadSize.HasValue)
                    initResp.options.uploadMaxSize = $"{currentVolume.MaxUploadSizeInKb.Value}K";

                openResp = initResp;
            }
            else
            {
                cwd = targetPath.Directory;
                cwdHash = cwd.GetHash(currentVolume, _pathParser);
                cwdParentHash = cwd.GetParentHash(currentVolume, _pathParser);
                openResp = new OpenResponse(await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, cancellationToken),
                    new ConnectorOptions(targetPath, '/'));
            }

            foreach (var item in (await cwd.GetFilesAsync(cmd.Mimes, true, cancellationToken)))
            {
                openResp.files.Add(await item.ToFileInfoAsync(cwdHash, currentVolume, _pathParser, _pictureEditor, cancellationToken));
            }

            foreach (var item in (await cwd.GetDirectoriesAsync(cancellationToken: cancellationToken)))
            {
                var hash = item.GetHash(currentVolume, _pathParser);
                openResp.files.Add(await item.ToFileInfoAsync(hash, cwdHash, currentVolume, cancellationToken));
            }

            if (cmd.Tree == 1)
            {
                foreach (var volume in _volumes)
                {
                    if (targetPath.IsRoot && volume == targetPath.Volume) continue;

                    var rootVolumeDir = new FileSystemDirectory(volume.RootDirectory, volume);
                    var hash = rootVolumeDir.GetHash(volume, _pathParser);
                    openResp.files.Add(await rootVolumeDir.ToFileInfoAsync(hash, null, volume, cancellationToken));
                }
            }

            return openResp;
        }

        public async Task<InfoResponse> InfoAsync(InfoCommand cmd, CancellationToken cancellationToken = default)
        {
            var infoResp = new InfoResponse();
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();

            foreach (var target in cmd.TargetPaths)
            {
                var targetHash = target.HashedTarget;
                var phash = target.FileSystem.GetParentHash(volume, _pathParser);

                try
                {
                    if (target.IsDirectory)
                        infoResp.files.Add(await target.Directory.ToFileInfoAsync(targetHash, phash, volume, cancellationToken));
                    else
                        infoResp.files.Add(await target.File.ToFileInfoAsync(phash, volume, _pathParser, _pictureEditor, cancellationToken));
                }
                catch (Exception) { }
            }

            return infoResp;
        }

        public async Task<ParentsResponse> ParentsAsync(ParentsCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parentsResp = new ParentsResponse();
            var targetPath = cmd.TargetPath;
            var targetDir = targetPath.Directory;
            var volume = targetPath.Volume;
            var targetHash = targetPath.HashedTarget;

            if (targetPath.IsRoot)
            {
                parentsResp.tree.Add(await targetDir.ToFileInfoAsync(targetHash, null, volume, cancellationToken));
            }
            else
            {
                await AddParentsToListAsync(targetPath, parentsResp.tree, cancellationToken);
            }

            return parentsResp;
        }

        public Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = volume.RootDirectory + decodedPath;

            if (Directory.Exists(fullPath) || (createIfNotExists && !fileByDefault)) return Task.FromResult(
                 new PathInfo(decodedPath, volume, new FileSystemDirectory(fullPath, volume), hashedTarget));

            if (File.Exists(fullPath) || (createIfNotExists && fileByDefault)) return Task.FromResult(
                 new PathInfo(decodedPath, volume, new FileSystemFile(fullPath, volume), hashedTarget));

            if (fileByDefault)
                throw new FileNotFoundException();
            throw new DirectoryNotFoundException();
        }

        public async Task<RenameResponse> RenameAsync(RenameCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var renameResp = new RenameResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (targetPath.IsDirectory)
            {
                var renamedDir = await targetPath.Directory.RenameAsync(cmd.Name, cancellationToken);
                var hash = renamedDir.GetHash(volume, _pathParser);
                var phash = renamedDir.GetParentHash(volume, _pathParser);
                renameResp.added.Add(await renamedDir.ToFileInfoAsync(hash, phash, volume, cancellationToken));
            }
            else
            {
                var renamedFile = await targetPath.File.RenameAsync(cmd.Name, cancellationToken);
                var phash = renamedFile.GetParentHash(volume, _pathParser);
                renameResp.added.Add(await renamedFile.ToFileInfoAsync(phash, volume, _pathParser, _pictureEditor, cancellationToken));
            }

            renameResp.removed.Add(targetPath.HashedTarget);
            await RemoveThumbsAsync(targetPath, cancellationToken);

            return renameResp;
        }

        public async Task<RmResponse> RmAsync(RmCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rmResp = new RmResponse();

            foreach (var path in cmd.TargetPaths)
            {
                if (path.IsDirectory)
                {
                    if (await path.Directory.ExistsAsync) await path.Directory.DeleteAsync(cancellationToken);
                }
                else if (await path.File.ExistsAsync) await path.File.DeleteAsync(cancellationToken);

                await RemoveThumbsAsync(path, cancellationToken);

                rmResp.removed.Add(path.HashedTarget);
            }

            return rmResp;
        }

        public async Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (volume.ThumbnailDirectory != null)
            {
                var tmbDirObj = new FileSystemDirectory(volume.ThumbnailDirectory, volume);

                if (!await tmbDirObj.ExistsAsync)
                    await tmbDirObj.CreateAsync(cancellationToken);

                if (!tmbDirObj.Attributes.HasFlag(FileAttributes.Hidden))
                    tmbDirObj.Attributes = FileAttributes.Hidden;
            }

            if (!Directory.Exists(volume.RootDirectory))
            {
                Directory.CreateDirectory(volume.RootDirectory);
            }
        }

        public async Task<TmbResponse> TmbAsync(TmbCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tmbResp = new TmbResponse();
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();

            foreach (var path in cmd.TargetPaths)
            {
                var thumbPath = await volume.GenerateThumbPathAsync(path.File, _pictureEditor, cancellationToken);
                if (thumbPath == null) continue;

                if (!File.Exists(thumbPath) && path.File.ObjectAttribute.Read)
                    await new FileSystemFile(thumbPath, volume)
                        .CreateThumbAsync(path.File.FullName, volume.ThumbnailSize, _pictureEditor, cancellationToken);

                var thumbUrl = volume.GetPathUrl(thumbPath);
                tmbResp.images.Add(path.HashedTarget, thumbUrl);
            }

            return tmbResp;
        }

        public async Task<UploadResponse> UploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uploadResp = new UploadResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (cmd.Renames.Any())
                throw new CommandNoSupportException();

            var warning = new HashSet<string>();
            var warningDetails = new List<ErrorResponse>();
            var setNewParents = new HashSet<IDirectory>();
            foreach (var uploadPath in cmd.UploadPathInfos.Distinct())
            {
                var directory = uploadPath.Directory;
                string lastParentHash = null;

                while (!volume.IsRoot(directory))
                {
                    var hash = lastParentHash ?? directory.GetHash(volume, _pathParser);
                    lastParentHash = directory.GetParentHash(volume, _pathParser);

                    if (!await directory.ExistsAsync && setNewParents.Add(directory))
                        uploadResp.added.Add(await directory.ToFileInfoAsync(hash, lastParentHash, volume, cancellationToken));

                    directory = directory.Parent;
                }
            }

            var uploadCount = cmd.Upload.Count();
            for (var idx = 0; idx < uploadCount; idx++)
            {
                var formFile = cmd.Upload.ElementAt(idx);
                IDirectory dest;
                string destHash;

                try
                {
                    if (cmd.UploadPath.Count > idx)
                    {
                        dest = cmd.UploadPathInfos.ElementAt(idx).Directory;
                        destHash = cmd.UploadPath[idx];
                    }
                    else
                    {
                        dest = targetPath.Directory;
                        destHash = targetPath.HashedTarget;
                    }

                    if (!dest.CanCreateObject())
                        throw new PermissionDeniedException($"Permission denied: {volume.GetRelativePath(dest)}");

                    string uploadFileName = Path.Combine(dest.FullName, Path.GetFileName(formFile.FileName));
                    var uploadFileInfo = new FileSystemFile(uploadFileName, volume);

                    if (await uploadFileInfo.ExistsAsync)
                    {
                        if (cmd.Overwrite == 0 || (cmd.Overwrite == null && !volume.UploadOverwrite))
                        {
                            string newName = await uploadFileInfo.GetCopyNameAsync(cmd.Suffix, cancellationToken);
                            uploadFileName = Path.Combine(uploadFileInfo.DirectoryName, newName);
                            uploadFileInfo = new FileSystemFile(uploadFileName, volume);
                        }
                        else if (!uploadFileInfo.ObjectAttribute.Write)
                            throw new PermissionDeniedException();
                    }

                    using (var fileStream = await uploadFileInfo.OpenWriteAsync(cancellationToken))
                    {
                        await formFile.CopyToAsync(fileStream, cancellationToken);
                    }

                    await uploadFileInfo.RefreshAsync(cancellationToken);
                    uploadResp.added.Add(await uploadFileInfo.ToFileInfoAsync(destHash, volume, _pathParser, _pictureEditor, cancellationToken));
                }
                catch (PermissionDeniedException ex)
                {
                    warning.Add(string.IsNullOrEmpty(ex.Message) ? $"Permission denied: {formFile.FileName}" : ex.Message);
                    warningDetails.Add(ErrorResponse.Factory.UploadFile(ex, formFile.FileName));
                }
                catch (Exception ex)
                {
                    warning.Add($"Failed to upload: {formFile.FileName}");
                    warningDetails.Add(ErrorResponse.Factory.UploadFile(ex, formFile.FileName));
                }
            }

            if (warning.Any())
            {
                uploadResp.warning = warning.Select(o => o as object).ToList();
                uploadResp.SetWarningDetails(warningDetails);
            }

            return uploadResp;
        }

        public async Task<TreeResponse> TreeAsync(TreeCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var treeResp = new TreeResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
            {
                var hash = item.GetHash(volume, _pathParser);
                treeResp.tree.Add(await item.ToFileInfoAsync(hash, targetPath.HashedTarget, volume, cancellationToken));
            }

            return treeResp;
        }

        public async Task<SizeResponse> SizeAsync(SizeCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sizeResp = new SizeResponse();

            foreach (var path in cmd.TargetPaths)
            {
                if (!path.FileSystem.ObjectAttribute.Read) throw new PermissionDeniedException();

                if (path.IsDirectory)
                {
                    sizeResp.dirCnt++;

                    var sizeAndCount = await path.Directory.GetSizeAndCountAsync(cancellationToken: cancellationToken);
                    sizeResp.dirCnt += sizeAndCount.DirectoryCount;
                    sizeResp.fileCnt += sizeAndCount.FileCount;
                    sizeResp.size += sizeAndCount.Size;
                }
                else
                {
                    sizeResp.fileCnt++;
                    sizeResp.size += await path.File.LengthAsync;
                }
            }

            return sizeResp;
        }

        public async Task<DimResponse> DimAsync(DimCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = cmd.TargetPath.File;

            if (!file.CanEditImage()) throw new PermissionDeniedException();

            using (var stream = await file.OpenReadAsync(cancellationToken))
            {
                var size = _pictureEditor.ImageSize(stream);
                return new DimResponse(size);
            }
        }

        public async Task<FileResponse> FileAsync(FileCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = cmd.TargetPath;
            var file = targetPath.File;

            if (!file.CanDownload()) throw new PermissionDeniedException();

            return new FileResponse
            {
                ContentType = MimeHelper.GetMimeType(file.Extension),
                FileStream = await file.OpenReadAsync(cancellationToken),
                FileDownloadName = file.Name,
                ContentDisposition = cmd.Download == 1 ? DispositionTypeNames.Attachment : DispositionTypeNames.Inline
            };
        }

        public async Task<GetResponse> GetAsync(GetCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = new GetResponse();
            var targetPath = cmd.TargetPath;
            var conv = cmd.Conv;
            var autoConv = conv == "1" || conv == "0";

            if (!targetPath.File.ObjectAttribute.Read) throw new PermissionDeniedException();

            try
            {
                var baseStream = await targetPath.File.OpenReadAsync(cancellationToken);
                var reader = autoConv ? new StreamReader(baseStream, true) :
                    new StreamReader(baseStream, Encoding.GetEncoding(conv));
                using (reader)
                {
                    response.content = await GetInlineContentAsync(targetPath.File, reader, cancellationToken);
                    response.doconv = null;

                    response.encoding = autoConv ? reader.CurrentEncoding.WebName.ToUpperInvariant() : conv;
                    if (response.encoding.Equals(Encoding.UTF8.WebName, StringComparison.InvariantCultureIgnoreCase))
                        response.encoding = null;
                }
            }
            catch (Exception ex)
            {
                response.SetException(ex);

                if (ex is NotSupportedException || ex is ArgumentException)
                    switch (cmd.Conv)
                    {
                        case "1":
                            response.content = false;
                            break;
                        case "0":
                            response.doconv = "unknown";
                            break;
                        default:
                            response.encoding = conv;
                            break;
                    }
                else throw ex;
            }

            return response;
        }

        public async Task<PasteResponse> PasteAsync(PasteCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pasteResp = new PasteResponse();
            var isCut = cmd.Cut == 1;
            var dstPath = cmd.DstPath;

            if (cmd.Renames.Any())
                throw new CommandNoSupportException();

            foreach (var src in cmd.TargetPaths)
            {
                if (src.IsDirectory)
                {
                    IDirectory pastedDir;
                    var newDest = Path.Combine(dstPath.Directory.FullName, src.Directory.Name);

                    if (isCut)
                    {
                        if (Directory.Exists(newDest))
                        {
                            await src.Directory.MergeAsync(newDest, dstPath.Volume.CopyOverwrite, cancellationToken: cancellationToken);
                            await src.Directory.DeleteAsync(cancellationToken);
                            pastedDir = new FileSystemDirectory(newDest, dstPath.Volume);
                        }
                        else pastedDir = await src.Directory.MoveToAsync(newDest, cancellationToken);

                        await RemoveThumbsAsync(src, cancellationToken);

                        pasteResp.removed.Add(src.HashedTarget);
                    }
                    else
                    {
                        pastedDir = await src.Directory.CopyToAsync(newDest, dstPath.Volume.CopyOverwrite, cancellationToken: cancellationToken);
                    }

                    if (pastedDir != null)
                    {
                        var hash = pastedDir.GetHash(dstPath.Volume, _pathParser);
                        pasteResp.added.Add(await pastedDir.ToFileInfoAsync(hash, dstPath.HashedTarget, dstPath.Volume, cancellationToken));
                    }
                }
                else
                {
                    IFile pastedFile;

                    if (isCut)
                    {
                        pastedFile = await src.File.SafeMoveToAsync(dstPath.Directory.FullName, dstPath.Volume.CopyOverwrite, cancellationToken: cancellationToken);
                        await RemoveThumbsAsync(src, cancellationToken);
                        pasteResp.removed.Add(src.HashedTarget);
                    }
                    else
                    {
                        pastedFile = await src.File.SafeCopyToAsync(dstPath.Directory.FullName, dstPath.Volume.CopyOverwrite, cancellationToken: cancellationToken);
                    }

                    pasteResp.added.Add(await pastedFile.ToFileInfoAsync(dstPath.HashedTarget, dstPath.Volume, _pathParser, _pictureEditor, cancellationToken));
                }
            }

            return pasteResp;
        }

        public async Task<DuplicateResponse> DuplicateAsync(DuplicateCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dupResp = new DuplicateResponse();

            foreach (var src in cmd.TargetPaths)
            {
                if (src.IsDirectory)
                {
                    var newName = await src.Directory.GetCopyNameAsync(cancellationToken: cancellationToken);
                    var dupDir = await src.Directory.CopyToAsync(Path.Combine(src.Directory.Parent.FullName, newName),
                        src.Volume.CopyOverwrite, cancellationToken: cancellationToken);

                    var hash = dupDir.GetHash(src.Volume, _pathParser);
                    var parentHash = dupDir.GetParentHash(src.Volume, _pathParser);
                    dupResp.added.Add(await dupDir.ToFileInfoAsync(hash, parentHash, src.Volume, cancellationToken));
                }
                else
                {
                    var dupFile = await src.File.SafeCopyToAsync(src.File.Parent.FullName, src.Volume.CopyOverwrite, cancellationToken: cancellationToken);

                    var parentHash = src.File.GetParentHash(src.Volume, _pathParser);
                    dupResp.added.Add(await dupFile.ToFileInfoAsync(parentHash, src.Volume, _pathParser, _pictureEditor, cancellationToken));
                }
            }

            return dupResp;
        }

        public async Task<SearchResponse> SearchAsync(SearchCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchResp = new SearchResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetFilesAsync(cmd.Q, cmd.Mimes,
                searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
            {
                var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                    item.GetParentHash(volume, _pathParser);
                searchResp.files.Add(await item.ToFileInfoAsync(parentHash, volume, _pathParser, _pictureEditor, cancellationToken));
            }

            if (cmd.Mimes.Count == 0)
            {
                foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cmd.Q,
                    searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
                {
                    var hash = item.GetHash(volume, _pathParser);
                    var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                        item.GetParentHash(volume, _pathParser);
                    searchResp.files.Add(await item.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
                }
            }

            return searchResp;
        }

        public async Task<ArchiveResponse> ArchiveAsync(ArchiveCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cmd.Type != MediaTypeNames.Application.Zip)
                throw new ArchiveTypeException();

            var archiveResp = new ArchiveResponse();
            var targetPath = cmd.TargetPath;
            var volume = cmd.TargetPath.Volume;
            var directoryInfo = targetPath.Directory;

            var filename = cmd.Name ?? targetPath.Directory.Name;
            var zipExt = $".{FileExtensions.Zip}";

            if (!filename.EndsWith(zipExt))
                filename += zipExt;

            var newPath = Path.Combine(directoryInfo.FullName, filename);
            var newFile = new FileSystemFile(newPath, volume);

            if (!await newFile.CanArchiveToAsync())
                throw new PermissionDeniedException();

            using (var fileStream = ZipFile.Open(newPath, ZipArchiveMode.Update))
            {
                foreach (var path in cmd.TargetPaths)
                {
                    if (!path.FileSystem.CanBeArchived()) throw new PermissionDeniedException();

                    if (path.IsDirectory)
                    {
                        await fileStream.AddDirectoryAsync(path.Directory, fromDir: string.Empty, false, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        fileStream.CreateEntryFromFile(path.File.FullName, path.File.Name);
                    }
                }
            }

            await newFile.RefreshAsync(cancellationToken);
            archiveResp.added.Add(await newFile.ToFileInfoAsync(targetPath.HashedTarget, volume, _pathParser, _pictureEditor, cancellationToken));
            return archiveResp;
        }

        public async Task<ExtractResponse> ExtractAsync(ExtractCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractResp = new ExtractResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetParent = targetPath.File.Parent;
            var fromPath = targetParent.FullName;
            var makedir = cmd.MakeDir == 1;

            if (!targetPath.File.CanExtract()) throw new PermissionDeniedException();

            if (makedir)
            {
                fromPath = Path.Combine(fromPath, Path.GetFileNameWithoutExtension(targetPath.File.Name));
                var fromDir = new FileSystemDirectory(fromPath, volume);

                if (!await fromDir.CanExtractToAsync())
                    throw new PermissionDeniedException();

                if (!await fromDir.ExistsAsync)
                    await fromDir.CreateAsync(cancellationToken);

                var hash = fromDir.GetHash(volume, _pathParser);
                var parentHash = fromDir.GetParentHash(volume, _pathParser);
                extractResp.added.Add(await fromDir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
            }

            using (var archive = ZipFile.OpenRead(targetPath.File.FullName))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string fullName = PathHelper.GetFullPathNormalized(fromPath, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        var dir = new FileSystemDirectory(fullName, volume);
                        if (!await dir.CanExtractToAsync())
                            throw new PermissionDeniedException();

                        if (!await dir.ExistsAsync)
                            await dir.CreateAsync(cancellationToken);

                        if (!makedir)
                        {
                            var parentHash = dir.GetParentHash(volume, _pathParser);
                            var hash = dir.GetHash(volume, _pathParser);
                            extractResp.added.Add(await dir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
                        }
                    }
                    else
                    {
                        var file = new FileSystemFile(fullName, volume);

                        if (!await file.CanExtractToAsync()) throw new PermissionDeniedException();

                        entry.ExtractToFile(fullName, true);

                        if (!makedir)
                        {
                            await file.RefreshAsync(cancellationToken);
                            var parentHash = file.GetParentHash(volume, _pathParser);
                            extractResp.added.Add(await file.ToFileInfoAsync(parentHash, volume, _pathParser, _pictureEditor, cancellationToken));
                        }
                    }
                }
            }

            return extractResp;
        }

        public async Task<PutResponse> PutAsync(PutCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var putResp = new PutResponse();
            var targetPath = cmd.TargetPath;
            var targetFile = targetPath.File;
            var volume = targetPath.Volume;

            if (!targetFile.ObjectAttribute.Write) throw new PermissionDeniedException();

            if (cmd.Encoding == "scheme")
            {
                if (cmd.Content.StartsWith(WebConsts.UriScheme.Data))
                {
                    var data = ParseDataURIScheme(cmd.Content, nameof(ConnectorCommand.Cmd_Put));
                    using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken))
                    {
                        fileStream.Write(data, 0, data.Length);
                    }
                }
                else
                {
                    using (var client = new HttpClient())
                    {
                        var dataStream = await client.GetStreamAsync(cmd.Content);

                        using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken))
                        {
                            await dataStream.CopyToAsync(fileStream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken))
                using (var writer = new StreamWriter(fileStream, Encoding.GetEncoding(cmd.Encoding)))
                {
                    writer.Write(cmd.Content);
                }
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, _pathParser);
            putResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, _pathParser, _pictureEditor, cancellationToken));

            return putResp;
        }

        public async Task<ResizeResponse> ResizeAsync(ResizeCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resizeResp = new ResizeResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetFile = targetPath.File;

            if (!targetFile.CanEditImage()) throw new PermissionDeniedException();

            switch (cmd.Mode)
            {
                case ResizeCommand.Mode_Resize:
                    {
                        await RemoveThumbsAsync(targetPath, cancellationToken);

                        ImageWithMimeType image;
                        using (var stream = await targetFile.OpenReadAsync(cancellationToken))
                        {
                            image = _pictureEditor.Resize(stream, cmd.Width, cmd.Height, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                    break;
                case ResizeCommand.Mode_Crop:
                    {
                        await RemoveThumbsAsync(targetPath, cancellationToken);

                        ImageWithMimeType image;
                        using (var stream = await targetFile.OpenReadAsync(cancellationToken))
                        {
                            image = _pictureEditor.Crop(stream, cmd.X, cmd.Y,
                                cmd.Width, cmd.Height, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                    break;
                case ResizeCommand.Mode_Rotate:
                    {
                        await RemoveThumbsAsync(targetPath, cancellationToken);

                        ImageWithMimeType image;
                        using (var stream = await targetFile.OpenReadAsync(cancellationToken))
                        {
                            image = _pictureEditor.Rotate(stream, cmd.Degree, cmd.Background, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                    break;
                default:
                    throw new UnknownCommandException();
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, _pathParser);
            resizeResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, _pathParser, _pictureEditor, cancellationToken));
            return resizeResp;
        }

        public async Task<Zipdl1stResponse> ZipdlAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var zipdlData = new ZipdlData();
            var targetPaths = cmd.TargetPaths;
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();
            var zipExt = $".{FileExtensions.Zip}";

            var tempFile = Path.GetTempFileName();
            var tempFileName = Path.GetFileName(tempFile);
            var newFile = new FileSystemFile(tempFile, volume);

            try
            {
                using (var fileStream = ZipFile.Open(tempFile, ZipArchiveMode.Update))
                {
                    foreach (var path in cmd.TargetPaths)
                    {
                        if (!path.FileSystem.CanDownload())
                            throw new PermissionDeniedException();

                        if (path.IsDirectory)
                        {
                            await fileStream.AddDirectoryAsync(path.Directory,
                                fromDir: string.Empty, true, path.IsRoot ? volume.Name : null, cancellationToken);
                        }
                        else
                        {
                            fileStream.CreateEntryFromFile(path.File.FullName, path.File.Name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                throw e;
            }

            zipdlData.mime = MediaTypeNames.Application.Zip;
            zipdlData.name = DownloadHelper.GetZipDownloadName(cmd.TargetPaths) + zipExt;
            zipdlData.file = tempFileName;

            return new Zipdl1stResponse
            {
                zipdl = zipdlData
            };
        }

        public async Task<FileResponse> ZipdlRawAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tempDirPath = Path.GetTempPath();
            var tempFileInfo = new FileInfo(Path.Combine(tempDirPath, cmd.ArchiveFileKey));
            var memStream = new MemoryStream();

            using (var fileStream = tempFileInfo.OpenRead())
            {
                await fileStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken);
            }

            tempFileInfo.Delete();
            memStream.Position = 0;

            return new FileResponse
            {
                ContentType = cmd.MimeType,
                FileStream = memStream,
                FileDownloadName = cmd.DownloadFileName
            };
        }

        public Task<IVolume> FindOwnVolumeAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_volumes.FirstOrDefault(volume => volume.Own(fullPath)));
        }

        private async Task AddParentsToListAsync(PathInfo pathInfo, List<object> list, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDir = pathInfo.Directory;
            var volume = pathInfo.Volume;
            string lastParentHash = null;

            do
            {
                currentDir = currentDir.Parent;

                var hash = lastParentHash ?? currentDir.GetHash(volume, _pathParser);
                lastParentHash = currentDir.GetParentHash(volume, _pathParser);

                foreach (var item in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var subHash = item.GetHash(volume, _pathParser);
                    list.Add(await item.ToFileInfoAsync(subHash, hash, volume, cancellationToken));
                }
            }
            while (!volume.IsRoot(currentDir));
        }

        private async Task RemoveThumbsAsync(PathInfo path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.IsDirectory)
            {
                string thumbPath = await path.Volume.GenerateThumbPathAsync(path.Directory, cancellationToken);
                if (!string.IsNullOrEmpty(thumbPath) && Directory.Exists(thumbPath))
                {
                    Directory.Delete(thumbPath, true);
                }
            }
            else
            {
                string thumbPath = await path.Volume.GenerateThumbPathAsync(path.File, _pictureEditor, cancellationToken);
                if (!string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                }
            }
        }

        private async Task<string> GetInlineContentAsync(IFile file, StreamReader reader, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mime = MimeHelper.GetMimeType(file.Extension);

            if (mime.Type == ContentTypeNames.Text.Type)
                return reader.ReadToEnd();

            using (var memStream = new MemoryStream())
            {
                await reader.BaseStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken);
                var base64Str = Convert.ToBase64String(memStream.ToArray());
                var dataUri = $"data:{mime};charset={reader.CurrentEncoding.WebName};base64,{base64Str}";
                return dataUri;
            }
        }

        private byte[] ParseDataURIScheme(string dataUri, string fromcmd)
        {
            var parts = dataUri.Split(',');
            if (parts.Length != 2)
                throw new CommandParamsException(fromcmd);

            return Convert.FromBase64String(parts[1]);
        }
    }
}
