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
using elFinder.Net.Drivers.FileSystem.Factories;
using elFinder.Net.Drivers.FileSystem.Helpers;
using elFinder.Net.Drivers.FileSystem.Services;
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
        protected readonly IPathParser pathParser;
        protected readonly IPictureEditor pictureEditor;
        protected readonly IList<IVolume> volumes;
        protected readonly IZipDownloadPathProvider zipDownloadPathProvider;
        protected readonly IFileSystemFileFactory fileFactory;
        protected readonly IFileSystemDirectoryFactory directoryFactory;
        protected readonly IZipFileArchiver zipFileArchiver;
        protected readonly IConnector connector;

        public FileSystemDriver(IPathParser pathParser,
            IPictureEditor pictureEditor,
            IZipDownloadPathProvider zipDownloadPathProvider,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory,
            IZipFileArchiver zipFileArchiver,
            IConnector connector)
        {
            this.pathParser = pathParser;
            this.pictureEditor = pictureEditor;
            this.zipDownloadPathProvider = zipDownloadPathProvider;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
            this.zipFileArchiver = zipFileArchiver;
            this.connector = connector;
            volumes = new List<IVolume>();
        }

        public virtual void AddVolume(IVolume volume)
        {
            volumes.Add(volume);
        }

        public virtual IFile CreateFileObject(string path, IVolume volume)
        {
            return fileFactory.Create(path, volume, directoryFactory);
        }

        public virtual async Task<LsResponse> LsAsync(LsCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lsResp = new LsResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetFilesAsync(cmd.Mimes, true, cancellationToken))
            {
                string itemName = item.Name;
                if (cmd.Intersect.Count > 0)
                {
                    itemName = cmd.Intersect.FirstOrDefault(intersectItem => intersectItem.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
                    if (itemName == null) continue;
                }

                var hash = item.GetHash(volume, pathParser);
                lsResp.list[hash] = itemName;
            }

            foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
            {
                string itemName = item.Name;
                if (cmd.Intersect.Count > 0)
                {
                    itemName = cmd.Intersect.FirstOrDefault(intersectItem => intersectItem.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));
                    if (itemName == null) continue;
                }

                var hash = item.GetHash(volume, pathParser);
                lsResp.list[hash] = itemName;
            }

            return lsResp;
        }

        public virtual async Task<MkdirResponse> MkdirAsync(MkdirCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mkdirResp = new MkdirResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetHash = targetPath.HashedTarget;

            if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

            if (!string.IsNullOrEmpty(cmd.Name))
            {
                var newDir = directoryFactory.Create(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume, fileFactory);
                await newDir.CreateAsync(cancellationToken);
                var hash = newDir.GetHash(volume, pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, targetHash, volume, cancellationToken));
            }

            foreach (string dir in cmd.Dirs)
            {
                string dirName = dir.StartsWith("/") ? dir.Substring(1) : dir;
                var newDir = directoryFactory.Create(Path.Combine(targetPath.Directory.FullName, dirName), volume, fileFactory);
                await newDir.CreateAsync(cancellationToken);

                var hash = newDir.GetHash(volume, pathParser);
                var parentHash = newDir.GetParentHash(volume, pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));

                string relativePath = volume.GetRelativePath(newDir);
                mkdirResp.hashes.Add($"/{dirName}", volume.VolumeId + pathParser.Encode(relativePath));
            }

            return mkdirResp;
        }

        public virtual async Task<MkfileResponse> MkfileAsync(MkfileCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
            var targetHash = targetPath.HashedTarget;

            if (!targetPath.Directory.CanCreateObject()) throw new PermissionDeniedException();

            var newFile = fileFactory.Create(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume, directoryFactory);
            await newFile.CreateAsync(cancellationToken);

            var mkfileResp = new MkfileResponse();
            mkfileResp.added.Add(await newFile.ToFileInfoAsync(targetHash, volume, pathParser, pictureEditor, cancellationToken));

            return mkfileResp;
        }

        public virtual async Task<OpenResponse> OpenAsync(OpenCommand cmd, CancellationToken cancellationToken = default)
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
                        cwd = directoryFactory.Create(currentVolume.StartDirectory, currentVolume, fileFactory);
                    }

                    if (cwd == null || !cwd.ObjectAttribute.Read)
                    {
                        cwd = directoryFactory.Create(currentVolume.RootDirectory, currentVolume, fileFactory);
                        cwdHash = cwd.GetHash(currentVolume, pathParser);
                        targetPath = new PathInfo(string.Empty, currentVolume, cwd, cwdHash);
                    }
                    else
                    {
                        cwdHash = cwd.GetHash(currentVolume, pathParser);
                        targetPath = new PathInfo(currentVolume.GetRelativePath(cwd), currentVolume, cwd, cwdHash);
                    }

                    cmd.TargetPath = targetPath;
                }
                else
                {
                    cwd = targetPath.Directory;
                    cwdHash = targetPath.HashedTarget;
                }

                cwdParentHash = cwd.GetParentHash(currentVolume, pathParser);
                var initResp = new InitResponse(await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, cancellationToken),
                    new ConnectorResponseOptions(targetPath, connector.Options.DisabledUICommands, '/'));

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
                cwdHash = cwd.GetHash(currentVolume, pathParser);
                cwdParentHash = cwd.GetParentHash(currentVolume, pathParser);
                openResp = new OpenResponse(await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, cancellationToken),
                    new ConnectorResponseOptions(targetPath, connector.Options.DisabledUICommands, '/'));
            }

            foreach (var item in (await cwd.GetFilesAsync(cmd.Mimes, true, cancellationToken)))
            {
                openResp.files.Add(await item.ToFileInfoAsync(cwdHash, currentVolume, pathParser, pictureEditor, cancellationToken));
            }

            foreach (var item in (await cwd.GetDirectoriesAsync(cancellationToken: cancellationToken)))
            {
                var hash = item.GetHash(currentVolume, pathParser);
                openResp.files.Add(await item.ToFileInfoAsync(hash, cwdHash, currentVolume, cancellationToken));
            }

            if (cmd.Tree == 1)
            {
                foreach (var volume in volumes)
                {
                    if (targetPath.IsRoot && volume == targetPath.Volume) continue;

                    var rootVolumeDir = directoryFactory.Create(volume.RootDirectory, volume, fileFactory);
                    var hash = rootVolumeDir.GetHash(volume, pathParser);
                    openResp.files.Add(await rootVolumeDir.ToFileInfoAsync(hash, null, volume, cancellationToken));
                }
            }

            return openResp;
        }

        public virtual async Task<InfoResponse> InfoAsync(InfoCommand cmd, CancellationToken cancellationToken = default)
        {
            var infoResp = new InfoResponse();
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();

            foreach (var target in cmd.TargetPaths)
            {
                var targetHash = target.HashedTarget;
                var phash = target.FileSystem.GetParentHash(volume, pathParser);

                try
                {
                    if (target.IsDirectory)
                        infoResp.files.Add(await target.Directory.ToFileInfoAsync(targetHash, phash, volume, cancellationToken));
                    else
                        infoResp.files.Add(await target.File.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, cancellationToken));
                }
                catch (Exception) { }
            }

            return infoResp;
        }

        public virtual async Task<ParentsResponse> ParentsAsync(ParentsCommand cmd, CancellationToken cancellationToken = default)
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

        public virtual Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = volume.RootDirectory + decodedPath;

            if (Directory.Exists(fullPath) || (createIfNotExists && !fileByDefault)) return Task.FromResult(
                 new PathInfo(decodedPath, volume, directoryFactory.Create(fullPath, volume, fileFactory), hashedTarget));

            if (File.Exists(fullPath) || (createIfNotExists && fileByDefault)) return Task.FromResult(
                 new PathInfo(decodedPath, volume, fileFactory.Create(fullPath, volume, directoryFactory), hashedTarget));

            if (fileByDefault)
                throw new FileNotFoundException();
            throw new DirectoryNotFoundException();
        }

        public virtual async Task<RenameResponse> RenameAsync(RenameCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var renameResp = new RenameResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (targetPath.IsDirectory)
            {
                var renamedDir = await targetPath.Directory.RenameAsync(cmd.Name, cancellationToken);
                var hash = renamedDir.GetHash(volume, pathParser);
                var phash = renamedDir.GetParentHash(volume, pathParser);
                renameResp.added.Add(await renamedDir.ToFileInfoAsync(hash, phash, volume, cancellationToken));
            }
            else
            {
                var renamedFile = await targetPath.File.RenameAsync(cmd.Name, cancellationToken);
                var phash = renamedFile.GetParentHash(volume, pathParser);
                renameResp.added.Add(await renamedFile.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, cancellationToken));
            }

            renameResp.removed.Add(targetPath.HashedTarget);
            await RemoveThumbsAsync(targetPath, cancellationToken);

            return renameResp;
        }

        public virtual async Task<RmResponse> RmAsync(RmCommand cmd, CancellationToken cancellationToken = default)
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

        public virtual async Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (volume.ThumbnailDirectory != null)
            {
                var tmbDirObj = directoryFactory.Create(volume.ThumbnailDirectory, volume, fileFactory);

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

        public virtual async Task<TmbResponse> TmbAsync(TmbCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tmbResp = new TmbResponse();
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();

            foreach (var path in cmd.TargetPaths)
            {
                var thumbPath = await volume.GenerateThumbPathAsync(path.File, pictureEditor, cancellationToken);
                if (thumbPath == null) continue;

                if (!File.Exists(thumbPath) && path.File.ObjectAttribute.Read)
                    await fileFactory.Create(thumbPath, volume, directoryFactory)
                        .CreateThumbAsync(path.File.FullName, volume.ThumbnailSize, pictureEditor, cancellationToken);

                var thumbUrl = volume.GetPathUrl(thumbPath);
                tmbResp.images.Add(path.HashedTarget, thumbUrl);
            }

            return tmbResp;
        }

        public virtual async Task<InitUploadData> InitUploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uploadResp = new UploadResponse();
            var listUploadData = new List<UploadData>();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (cmd.Renames.Any())
                throw new CommandNoSupportException();

            var warning = uploadResp.GetWarnings();
            var warningDetails = uploadResp.GetWarningDetails();
            var setNewParents = new HashSet<IDirectory>();
            foreach (var uploadPath in cmd.UploadPathInfos.Distinct())
            {
                var directory = uploadPath.Directory;
                string lastParentHash = null;

                while (!volume.IsRoot(directory))
                {
                    var hash = lastParentHash ?? directory.GetHash(volume, pathParser);
                    lastParentHash = directory.GetParentHash(volume, pathParser);

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
                    var uploadFileInfo = fileFactory.Create(uploadFileName, volume, directoryFactory);
                    var isOverwrite = false;

                    if (await uploadFileInfo.ExistsAsync)
                    {
                        if (cmd.Overwrite == 0 || (cmd.Overwrite == null && !volume.UploadOverwrite))
                        {
                            string newName = await uploadFileInfo.GetCopyNameAsync(cmd.Suffix, cancellationToken);
                            uploadFileName = Path.Combine(uploadFileInfo.DirectoryName, newName);
                            uploadFileInfo = fileFactory.Create(uploadFileName, volume, directoryFactory);
                        }
                        else if (!uploadFileInfo.ObjectAttribute.Write)
                            throw new PermissionDeniedException();
                        else isOverwrite = true;
                    }

                    listUploadData.Add(new UploadData
                    {
                        Destination = uploadFileInfo,
                        FormFile = formFile,
                        DestinationHash = destHash,
                        IsOverwrite = isOverwrite
                    });
                }
                catch (Exception ex)
                {
                    var rootCause = ex.GetRootCause();

                    if (rootCause is PermissionDeniedException pEx)
                    {
                        warning.Add(string.IsNullOrEmpty(pEx.Message) ? $"Permission denied: {formFile.FileName}" : pEx.Message);
                        warningDetails.Add(ErrorResponse.Factory.UploadFile(pEx, formFile.FileName));
                    }
                    else
                    {
                        warning.Add($"Failed to upload: {formFile.FileName}");
                        warningDetails.Add(ErrorResponse.Factory.UploadFile(ex, formFile.FileName));
                    }
                }
            }

            return new InitUploadData
            {
                Data = listUploadData,
                Response = uploadResp,
                Volume = volume
            };
        }

        public virtual async Task UploadAsync(UploadData uploadData, InitUploadData initData, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uploadResp = initData.Response;
            var warning = uploadResp.GetWarnings();
            var warningDetails = uploadResp.GetWarningDetails();

            try
            {
                using (var fileStream = await uploadData.Destination.OpenWriteAsync(cancellationToken: cancellationToken))
                {
                    await uploadData.FormFile.CopyToAsync(fileStream, cancellationToken);
                }

                await uploadData.Destination.RefreshAsync(cancellationToken);
                uploadResp.added.Add(await uploadData.Destination.ToFileInfoAsync(uploadData.DestinationHash, initData.Volume, pathParser, pictureEditor, cancellationToken));
            }
            catch (Exception ex)
            {
                var rootCause = ex.GetRootCause();

                if (rootCause is PermissionDeniedException pEx)
                {
                    warning.Add(string.IsNullOrEmpty(pEx.Message) ? $"Permission denied: {uploadData.FormFile.FileName}" : pEx.Message);
                    warningDetails.Add(ErrorResponse.Factory.UploadFile(pEx, uploadData.FormFile.FileName));
                }
                else
                {
                    warning.Add($"Failed to upload: {uploadData.FormFile.FileName}");
                    warningDetails.Add(ErrorResponse.Factory.UploadFile(ex, uploadData.FormFile.FileName));
                }
            }
        }

        public virtual async Task<TreeResponse> TreeAsync(TreeCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var treeResp = new TreeResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cancellationToken: cancellationToken))
            {
                var hash = item.GetHash(volume, pathParser);
                treeResp.tree.Add(await item.ToFileInfoAsync(hash, targetPath.HashedTarget, volume, cancellationToken));
            }

            return treeResp;
        }

        public virtual async Task<SizeResponse> SizeAsync(SizeCommand cmd, CancellationToken cancellationToken = default)
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

        public virtual async Task<DimResponse> DimAsync(DimCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = cmd.TargetPath.File;

            if (!file.ObjectAttribute.Read) throw new PermissionDeniedException();

            using (var stream = await file.OpenReadAsync(cancellationToken))
            {
                var size = pictureEditor.ImageSize(stream);
                return new DimResponse(size);
            }
        }

        public virtual async Task<FileResponse> FileAsync(FileCommand cmd, CancellationToken cancellationToken = default)
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

        public virtual async Task<GetResponse> GetAsync(GetCommand cmd, CancellationToken cancellationToken = default)
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

        public virtual async Task<PasteResponse> PasteAsync(PasteCommand cmd, CancellationToken cancellationToken = default)
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
                            pastedDir = directoryFactory.Create(newDest, dstPath.Volume, fileFactory);
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
                        var hash = pastedDir.GetHash(dstPath.Volume, pathParser);
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

                    pasteResp.added.Add(await pastedFile.ToFileInfoAsync(dstPath.HashedTarget, dstPath.Volume, pathParser, pictureEditor, cancellationToken));
                }
            }

            return pasteResp;
        }

        public virtual async Task<DuplicateResponse> DuplicateAsync(DuplicateCommand cmd, CancellationToken cancellationToken = default)
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

                    var hash = dupDir.GetHash(src.Volume, pathParser);
                    var parentHash = dupDir.GetParentHash(src.Volume, pathParser);
                    dupResp.added.Add(await dupDir.ToFileInfoAsync(hash, parentHash, src.Volume, cancellationToken));
                }
                else
                {
                    var dupFile = await src.File.SafeCopyToAsync(src.File.Parent.FullName, src.Volume.CopyOverwrite, cancellationToken: cancellationToken);

                    var parentHash = src.File.GetParentHash(src.Volume, pathParser);
                    dupResp.added.Add(await dupFile.ToFileInfoAsync(parentHash, src.Volume, pathParser, pictureEditor, cancellationToken));
                }
            }

            return dupResp;
        }

        public virtual async Task<SearchResponse> SearchAsync(SearchCommand cmd, CancellationToken cancellationToken = default)
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
                    item.GetParentHash(volume, pathParser);
                searchResp.files.Add(await item.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, cancellationToken));
            }

            if (cmd.Mimes.Count == 0)
            {
                foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cmd.Q,
                    searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
                {
                    var hash = item.GetHash(volume, pathParser);
                    var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                        item.GetParentHash(volume, pathParser);
                    searchResp.files.Add(await item.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
                }
            }

            return searchResp;
        }

        public virtual async Task<ArchiveResponse> ArchiveAsync(ArchiveCommand cmd, CancellationToken cancellationToken = default)
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

            var archivePath = Path.Combine(directoryInfo.FullName, filename);
            var newFile = fileFactory.Create(archivePath, volume, directoryFactory);

            if (!await newFile.CanArchiveToAsync())
                throw new PermissionDeniedException();

            if (newFile.DirectoryExists())
                throw new ExistsException(newFile.Name);

            try
            {
                using (var fileStream = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                {
                    foreach (var path in cmd.TargetPaths)
                    {
                        if (!path.FileSystem.CanBeArchived()) throw new PermissionDeniedException();

                        if (path.IsDirectory)
                        {
                            await zipFileArchiver.AddDirectoryAsync(fileStream, path.Directory, fromDir: string.Empty, false, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            zipFileArchiver.CreateEntryFromFile(fileStream, path.File, path.File.Name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                throw e;
            }

            await newFile.RefreshAsync(cancellationToken);
            archiveResp.added.Add(await newFile.ToFileInfoAsync(targetPath.HashedTarget, volume, pathParser, pictureEditor, cancellationToken));
            return archiveResp;
        }

        public virtual async Task<ExtractResponse> ExtractAsync(ExtractCommand cmd, CancellationToken cancellationToken = default)
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
                var fromDir = directoryFactory.Create(fromPath, volume, fileFactory);

                if (!await fromDir.CanExtractToAsync())
                    throw new PermissionDeniedException();

                if (fromDir.FileExists())
                    throw new ExistsException(fromDir.Name);

                if (!await fromDir.ExistsAsync)
                    await fromDir.CreateAsync(cancellationToken);

                var hash = fromDir.GetHash(volume, pathParser);
                var parentHash = fromDir.GetParentHash(volume, pathParser);
                extractResp.added.Add(await fromDir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
            }

            using (var archive = ZipFile.OpenRead(targetPath.File.FullName))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string fullName = PathHelper.GetFullPathNormalized(fromPath, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        var dir = directoryFactory.Create(fullName, volume, fileFactory);
                        if (!await dir.CanExtractToAsync())
                            throw new PermissionDeniedException();

                        if (dir.FileExists())
                            throw new ExistsException(dir.Name);

                        if (!await dir.ExistsAsync)
                            await dir.CreateAsync(cancellationToken);

                        if (!makedir)
                        {
                            var parentHash = dir.GetParentHash(volume, pathParser);
                            var hash = dir.GetHash(volume, pathParser);
                            extractResp.added.Add(await dir.ToFileInfoAsync(hash, parentHash, volume, cancellationToken));
                        }
                    }
                    else
                    {
                        var file = fileFactory.Create(fullName, volume, directoryFactory);

                        if (!await file.CanExtractToAsync()) throw new PermissionDeniedException();

                        if (file.DirectoryExists())
                            throw new ExistsException(file.Name);

                        await zipFileArchiver.ExtractToAsync(entry, file, true, cancellationToken);

                        if (!makedir)
                        {
                            await file.RefreshAsync(cancellationToken);
                            var parentHash = file.GetParentHash(volume, pathParser);
                            extractResp.added.Add(await file.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, cancellationToken));
                        }
                    }
                }
            }

            return extractResp;
        }

        public virtual async Task<PutResponse> PutAsync(PutCommand cmd, CancellationToken cancellationToken = default)
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
                    using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                    {
                        fileStream.Write(data, 0, data.Length);
                    }
                }
                else
                {
                    using (var client = new HttpClient())
                    {
                        var dataStream = await client.GetStreamAsync(cmd.Content);

                        using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                        {
                            await dataStream.CopyToAsync(fileStream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                using (var writer = new StreamWriter(fileStream, Encoding.GetEncoding(cmd.Encoding)))
                {
                    writer.Write(cmd.Content);
                }
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, pathParser);
            putResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, cancellationToken));

            return putResp;
        }

        public virtual async Task<ResizeResponse> ResizeAsync(ResizeCommand cmd, CancellationToken cancellationToken = default)
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
                            image = pictureEditor.Resize(stream, cmd.Width, cmd.Height, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
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
                            image = pictureEditor.Crop(stream, cmd.X, cmd.Y,
                                cmd.Width, cmd.Height, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
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
                            image = pictureEditor.Rotate(stream, cmd.Degree, cmd.Background, cmd.Quality);
                        }
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken);
                        }
                    }
                    break;
                default:
                    throw new UnknownCommandException();
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, pathParser);
            resizeResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, cancellationToken));
            return resizeResp;
        }

        public virtual async Task<Zipdl1stResponse> ZipdlAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var zipdlData = new ZipdlData();
            var targetPaths = cmd.TargetPaths;
            var volume = cmd.TargetPaths.Select(p => p.Volume).First();
            var zipExt = $".{FileExtensions.Zip}";

            var (archivePath, archiveFileKey) = await zipDownloadPathProvider.GetFileForArchivingAsync();
            var newFile = fileFactory.Create(archivePath, volume, directoryFactory);

            try
            {
                using (var fileStream = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                {
                    foreach (var path in cmd.TargetPaths)
                    {
                        if (!path.FileSystem.CanDownload())
                            throw new PermissionDeniedException();

                        if (path.IsDirectory)
                        {
                            await zipFileArchiver.AddDirectoryAsync(fileStream, path.Directory,
                                fromDir: string.Empty, true, path.IsRoot ? volume.Name : null, cancellationToken);
                        }
                        else
                        {
                            zipFileArchiver.CreateEntryFromFile(fileStream, path.File, path.File.Name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                throw e;
            }

            zipdlData.mime = MediaTypeNames.Application.Zip;
            zipdlData.name = DownloadHelper.GetZipDownloadName(cmd.TargetPaths) + zipExt;
            zipdlData.file = archiveFileKey;

            return new Zipdl1stResponse
            {
                zipdl = zipdlData
            };
        }

        public virtual async Task<FileResponse> ZipdlRawAsync(ZipdlCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var archiveFile = await zipDownloadPathProvider.ParseArchiveFileKeyAsync(cmd.ArchiveFileKey);
            var tempFileInfo = new FileInfo(archiveFile);
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

        public virtual Task<IVolume> FindOwnVolumeAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(volumes.FirstOrDefault(volume => volume.Own(fullPath)));
        }

        protected virtual async Task AddParentsToListAsync(PathInfo pathInfo, List<object> list, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDir = pathInfo.Directory;
            var volume = pathInfo.Volume;
            string lastParentHash = null;

            do
            {
                currentDir = currentDir.Parent;

                var hash = lastParentHash ?? currentDir.GetHash(volume, pathParser);
                lastParentHash = currentDir.GetParentHash(volume, pathParser);

                foreach (var item in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var subHash = item.GetHash(volume, pathParser);
                    list.Add(await item.ToFileInfoAsync(subHash, hash, volume, cancellationToken));
                }
            }
            while (!volume.IsRoot(currentDir));
        }

        protected virtual async Task RemoveThumbsAsync(PathInfo path, CancellationToken cancellationToken = default)
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
                string thumbPath = await path.Volume.GenerateThumbPathAsync(path.File, pictureEditor, cancellationToken);
                if (!string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                }
            }
        }

        protected virtual async Task<string> GetInlineContentAsync(IFile file, StreamReader reader, CancellationToken cancellationToken = default)
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

        protected virtual byte[] ParseDataURIScheme(string dataUri, string fromcmd)
        {
            var parts = dataUri.Split(',');
            if (parts.Length != 2)
                throw new CommandParamsException(fromcmd);

            return Convert.FromBase64String(parts[1]);
        }
    }
}
