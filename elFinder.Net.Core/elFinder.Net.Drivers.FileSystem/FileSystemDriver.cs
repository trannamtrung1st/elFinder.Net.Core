using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Http;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.FileInfo;
using elFinder.Net.Core.Models.Options;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Extensions;
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
        public const string DefaultThumbExt = ".png";

        protected readonly IPathParser pathParser;
        protected readonly IPictureEditor pictureEditor;
        protected readonly IVideoEditor videoEditor;
        protected readonly IZipDownloadPathProvider zipDownloadPathProvider;
        protected readonly IZipFileArchiver zipFileArchiver;
        protected readonly IThumbnailBackgroundGenerator thumbnailBackgroundGenerator;
        protected readonly IConnector connector;

        public event EventHandler<IDirectory> OnBeforeMakeDir;
        public event EventHandler<IDirectory> OnAfterMakeDir;
        public event EventHandler<IFile> OnBeforeMakeFile;
        public event EventHandler<IFile> OnAfterMakeFile;
        public event EventHandler<(IFileSystem FileSystem, string RenameTo)> OnBeforeRename;
        public event EventHandler<(IFileSystem FileSystem, string PrevName)> OnAfterRename;
        public event EventHandler<IFileSystem> OnBeforeRemove;
        public event EventHandler<IFileSystem> OnAfterRemove;
        public event EventHandler<(IFile File, IFormFileWrapper FormFile, bool IsOverwrite)> OnBeforeUpload;
        public event EventHandler<(IFile File, IFormFileWrapper FormFile, bool IsOverwrite)> OnAfterUpload;
        public event EventHandler<Exception> OnUploadError;
        public event EventHandler<(IFileSystem FileSystem, string NewDest, bool IsOverwrite)> OnBeforeMove;
        public event EventHandler<(IFileSystem FileSystem, IFileSystem NewFileSystem, bool IsOverwrite)> OnAfterMove;
        public event EventHandler<(IFileSystem FileSystem, string Dest, bool IsOverwrite)> OnBeforeCopy;
        public event EventHandler<(IFileSystem FileSystem, IFileSystem NewFileSystem, bool IsOverwrite)> OnAfterCopy;
        public event EventHandler<IFile> OnBeforeArchive;
        public event EventHandler<IFile> OnAfterArchive;
        public event EventHandler<(Exception Exception, IFile File)> OnArchiveError;
        public event EventHandler<(IDirectory Parent, IDirectory FromDir, IFile ArchivedFile)> OnBeforeExtract;
        public event EventHandler<(IDirectory Parent, IDirectory FromDir, IFile ArchivedFile)> OnAfterExtract;
        public event EventHandler<(ArchivedFileEntry Entry, IFile DestFile, bool IsOverwrite)> OnBeforeExtractFile;
        public event EventHandler<(ArchivedFileEntry Entry, IFile DestFile, bool IsOverwrite)> OnAfterExtractFile;
        public event EventHandler<(byte[] Data, IFile File)> OnBeforeWriteData;
        public event EventHandler<(byte[] Data, IFile File)> OnAfterWriteData;
        public event EventHandler<(Func<Task<Stream>> OpenStreamFunc, IFile File)> OnBeforeWriteStream;
        public event EventHandler<(Func<Task<Stream>> OpenStreamFunc, IFile File)> OnAfterWriteStream;
        public event EventHandler<(string Content, string Encoding, IFile File)> OnBeforeWriteContent;
        public event EventHandler<(string Content, string Encoding, IFile File)> OnAfterWriteContent;

        public FileSystemDriver(IPathParser pathParser,
            IPictureEditor pictureEditor,
            IVideoEditor videoEditor,
            IZipDownloadPathProvider zipDownloadPathProvider,
            IZipFileArchiver zipFileArchiver,
            IThumbnailBackgroundGenerator thumbnailBackgroundGenerator,
            IConnector connector)
        {
            this.pathParser = pathParser;
            this.pictureEditor = pictureEditor;
            this.videoEditor = videoEditor;
            this.zipDownloadPathProvider = zipDownloadPathProvider;
            this.zipFileArchiver = zipFileArchiver;
            this.thumbnailBackgroundGenerator = thumbnailBackgroundGenerator;
            this.connector = connector;
        }

        public virtual async Task<LsResponse> LsAsync(LsCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lsResp = new LsResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (!targetPath.Directory.ObjectAttribute.Read) throw new PermissionDeniedException();

            foreach (var item in await targetPath.Directory.GetFilesAsync(cmd.Mimes, verify: true, filter: null, cancellationToken: cancellationToken))
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
                var newDir = new FileSystemDirectory(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume);

                OnBeforeMakeDir?.Invoke(this, newDir);
                await newDir.CreateAsync(cancellationToken: cancellationToken);
                OnAfterMakeDir?.Invoke(this, newDir);

                var hash = newDir.GetHash(volume, pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, targetHash, volume, connector.Options, cancellationToken: cancellationToken));
            }

            foreach (string dir in cmd.Dirs)
            {
                string dirName = dir.StartsWith("/") ? dir.Substring(1) : dir;
                var newDir = new FileSystemDirectory(Path.Combine(targetPath.Directory.FullName, dirName), volume);

                OnBeforeMakeDir?.Invoke(this, newDir);
                await newDir.CreateAsync(cancellationToken: cancellationToken);
                OnAfterMakeDir?.Invoke(this, newDir);

                var hash = newDir.GetHash(volume, pathParser);
                var parentHash = newDir.GetParentHash(volume, pathParser);
                mkdirResp.added.Add(await newDir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));

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

            var newFile = new FileSystemFile(Path.Combine(targetPath.Directory.FullName, cmd.Name), volume);

            OnBeforeMakeFile?.Invoke(this, newFile);
            await newFile.CreateAsync(cancellationToken: cancellationToken);
            OnAfterMakeFile?.Invoke(this, newFile);

            var mkfileResp = new MkfileResponse();
            mkfileResp.added.Add(await newFile.ToFileInfoAsync(targetHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));

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
                        cwd = new FileSystemDirectory(currentVolume.StartDirectory, currentVolume);
                    }

                    if (cwd == null || !cwd.ObjectAttribute.Read)
                    {
                        cwd = new FileSystemDirectory(currentVolume.RootDirectory, currentVolume);
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
                var fileInfo = await cwd.ToFileInfoAsync(
                    cwdHash, cwdParentHash, currentVolume, connector.Options, cancellationToken: cancellationToken);

                InitResponse initResp;
                if (fileInfo is RootInfoResponse rootInfo)
                {
                    initResp = new InitResponse(rootInfo, rootInfo.options);
                }
                else
                {
                    initResp = new InitResponse(fileInfo,
                        new ConnectorResponseOptions(cwd, connector.Options.DisabledUICommands, currentVolume.DirectorySeparatorChar));
                    await AddParentsToListAsync(targetPath, initResp.files, cancellationToken: cancellationToken);
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
                var fileInfo = await cwd.ToFileInfoAsync(cwdHash, cwdParentHash, currentVolume, connector.Options, cancellationToken: cancellationToken);

                if (fileInfo is RootInfoResponse rootInfo)
                {
                    openResp = new OpenResponse(rootInfo, rootInfo.options);
                }
                else
                {
                    openResp = new OpenResponse(fileInfo,
                        new ConnectorResponseOptions(cwd, connector.Options.DisabledUICommands, currentVolume.DirectorySeparatorChar));
                }
            }

            foreach (var item in (await cwd.GetFilesAsync(cmd.Mimes, verify: true, filter: null, cancellationToken: cancellationToken)))
            {
                openResp.files.Add(await item.ToFileInfoAsync(cwdHash, currentVolume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
            }

            foreach (var item in (await cwd.GetDirectoriesAsync(cancellationToken: cancellationToken)))
            {
                var hash = item.GetHash(currentVolume, pathParser);
                openResp.files.Add(await item.ToFileInfoAsync(hash, cwdHash, currentVolume, connector.Options, cancellationToken: cancellationToken));
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
                        infoResp.files.Add(await target.Directory.ToFileInfoAsync(targetHash, phash, volume, connector.Options, cancellationToken: cancellationToken));
                    else
                        infoResp.files.Add(await target.File.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
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
                parentsResp.tree.Add(await targetDir.ToFileInfoAsync(targetHash, null, volume, connector.Options, cancellationToken: cancellationToken));
            }
            else
            {
                await AddParentsToListAsync(targetPath, parentsResp.tree, cancellationToken: cancellationToken);
            }

            return parentsResp;
        }

        public virtual Task<PathInfo> ParsePathAsync(string decodedPath, IVolume volume, string hashedTarget,
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

        public virtual async Task<RenameResponse> RenameAsync(RenameCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var renameResp = new RenameResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;

            if (targetPath.IsDirectory)
            {
                var prevName = targetPath.Directory.Name;

                OnBeforeRename?.Invoke(this, (targetPath.Directory, cmd.Name));
                var renamedDir = await targetPath.Directory.RenameAsync(cmd.Name, cancellationToken: cancellationToken);
                OnAfterRename?.Invoke(this, (targetPath.Directory, prevName));

                var hash = renamedDir.GetHash(volume, pathParser);
                var phash = renamedDir.GetParentHash(volume, pathParser);
                renameResp.added.Add(await renamedDir.ToFileInfoAsync(hash, phash, volume, connector.Options, cancellationToken: cancellationToken));
            }
            else
            {
                var prevName = targetPath.File.Name;

                OnBeforeRename?.Invoke(this, (targetPath.File, cmd.Name));
                var renamedFile = await targetPath.File.RenameAsync(cmd.Name, cancellationToken: cancellationToken);
                OnAfterRename?.Invoke(this, (targetPath.File, prevName));

                var phash = renamedFile.GetParentHash(volume, pathParser);
                renameResp.added.Add(await renamedFile.ToFileInfoAsync(phash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
            }

            renameResp.removed.Add(targetPath.HashedTarget);
            await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

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
                    if (await path.Directory.ExistsAsync)
                    {
                        OnBeforeRemove?.Invoke(this, path.Directory);
                        await path.Directory.DeleteAsync(cancellationToken: cancellationToken);
                        OnAfterRemove?.Invoke(this, path.Directory);
                    }
                }
                else if (await path.File.ExistsAsync)
                {
                    OnBeforeRemove?.Invoke(this, path.File);
                    await path.File.DeleteAsync(cancellationToken: cancellationToken);
                    OnAfterRemove?.Invoke(this, path.File);
                }

                await RemoveThumbsAsync(path, cancellationToken: cancellationToken);

                rmResp.removed.Add(path.HashedTarget);
            }

            return rmResp;
        }

        public virtual async Task SetupVolumeAsync(IVolume volume, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (volume.ThumbnailDirectory != null)
            {
                var tmbDirObj = new FileSystemDirectory(volume.ThumbnailDirectory, volume);

                if (!await tmbDirObj.ExistsAsync)
                    await tmbDirObj.CreateAsync(cancellationToken: cancellationToken);

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

            foreach (var target in cmd.TargetPaths)
            {
                if (target.IsDirectory) return null;

                var (thumb, _, _) = await CreateThumbAsync(target.File, cancellationToken: cancellationToken);

                if (thumb != null)
                {
                    using (thumb) { }
                }

                tmbResp.images.Add(target.HashedTarget, target.HashedTarget);
            }

            return tmbResp;
        }

        public virtual async Task<UploadResponse> UploadAsync(UploadCommand cmd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uploadResp = new UploadResponse();
            var targetPath = cmd.TargetPath;
            var volume = targetPath.Volume;
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
                        uploadResp.added.Add(await directory.ToFileInfoAsync(hash, lastParentHash, volume, connector.Options, cancellationToken: cancellationToken));

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

                    string uploadFullName = Path.Combine(dest.FullName, Path.GetFileName(formFile.FileName));
                    var uploadFileInfo = new FileSystemFile(uploadFullName, volume);
                    var isOverwrite = false;

                    if (await uploadFileInfo.ExistsAsync)
                    {
                        if (cmd.Renames.Contains(formFile.FileName))
                        {
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(formFile.FileName);
                            var ext = Path.GetExtension(formFile.FileName);
                            var backupName = $"{fileNameWithoutExt}{cmd.Suffix}{ext}";
                            var fullBakName = Path.Combine(uploadFileInfo.Parent.FullName, backupName);
                            var bakFile = new FileSystemFile(fullBakName, volume);

                            if (await bakFile.ExistsAsync)
                                backupName = await bakFile.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

                            var prevName = uploadFileInfo.Name;
                            OnBeforeRename?.Invoke(this, (uploadFileInfo, backupName));
                            await uploadFileInfo.RenameAsync(backupName, cancellationToken: cancellationToken);
                            OnAfterRename?.Invoke(this, (uploadFileInfo, prevName));

                            uploadResp.added.Add(await uploadFileInfo.ToFileInfoAsync(destHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
                            uploadFileInfo = new FileSystemFile(uploadFullName, volume);
                        }
                        else if (cmd.Overwrite == 0 || (cmd.Overwrite == null && !volume.UploadOverwrite))
                        {
                            string newName = await uploadFileInfo.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);
                            uploadFullName = Path.Combine(uploadFileInfo.DirectoryName, newName);
                            uploadFileInfo = new FileSystemFile(uploadFullName, volume);
                            isOverwrite = false;
                        }
                        else if (!uploadFileInfo.ObjectAttribute.Write)
                            throw new PermissionDeniedException();
                        else isOverwrite = true;
                    }

                    OnBeforeUpload?.Invoke(this, (uploadFileInfo, formFile, isOverwrite));
                    using (var fileStream = await uploadFileInfo.OpenWriteAsync(cancellationToken: cancellationToken))
                    {
                        await formFile.CopyToAsync(fileStream, cancellationToken: cancellationToken);
                    }
                    OnAfterUpload?.Invoke(this, (uploadFileInfo, formFile, isOverwrite));

                    await uploadFileInfo.RefreshAsync(cancellationToken);
                    uploadResp.added.Add(await uploadFileInfo.ToFileInfoAsync(destHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
                }
                catch (Exception ex)
                {
                    var rootCause = ex.GetRootCause();
                    OnUploadError?.Invoke(this, ex);

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

            return uploadResp;
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
                treeResp.tree.Add(await item.ToFileInfoAsync(hash, targetPath.HashedTarget, volume, connector.Options, cancellationToken: cancellationToken));
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

            using (var stream = await file.OpenReadAsync(cancellationToken: cancellationToken))
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
                FileStream = await file.OpenReadAsync(cancellationToken: cancellationToken),
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
                var baseStream = await targetPath.File.OpenReadAsync(cancellationToken: cancellationToken);
                var reader = autoConv ? new StreamReader(baseStream, true) :
                    new StreamReader(baseStream, Encoding.GetEncoding(conv));
                using (reader)
                {
                    response.content = await GetInlineContentAsync(targetPath.File, reader, cancellationToken: cancellationToken);
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
            var dstVolume = dstPath.Volume;
            var copyOverwrite = dstPath.Volume.CopyOverwrite;

            foreach (var src in cmd.TargetPaths)
            {
                if (src.IsDirectory)
                {
                    IDirectory pastedDir;
                    var newDest = Path.Combine(dstPath.Directory.FullName, src.Directory.Name);
                    var newDestDir = new FileSystemDirectory(newDest, dstPath.Volume);
                    var exists = await newDestDir.ExistsAsync;

                    if (exists && cmd.Renames.Contains(newDestDir.Name))
                    {
                        var backupName = $"{newDestDir.Name}{cmd.Suffix}";
                        var fullBakName = Path.Combine(newDestDir.Parent.FullName, backupName);
                        var bakDir = new FileSystemDirectory(fullBakName, newDestDir.Volume);

                        if (await bakDir.ExistsAsync)
                            backupName = await bakDir.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

                        var prevName = newDestDir.Name;
                        OnBeforeRename?.Invoke(this, (newDestDir, backupName));
                        await newDestDir.RenameAsync(backupName, cancellationToken: cancellationToken);
                        OnAfterRename?.Invoke(this, (newDestDir, prevName));

                        var hash = newDestDir.GetHash(newDestDir.Volume, pathParser);
                        pasteResp.added.Add(await newDestDir.ToFileInfoAsync(hash, dstPath.HashedTarget, newDestDir.Volume, connector.Options, cancellationToken: cancellationToken));
                        newDestDir = new FileSystemDirectory(newDest, dstPath.Volume);

                        exists = false;
                    }

                    if (isCut)
                    {
                        if (exists)
                        {
                            OnBeforeMove?.Invoke(this, (src.Directory, newDest, true));
                            pastedDir = await MergeAsync(src.Directory, newDest, dstVolume, copyOverwrite, cancellationToken: cancellationToken);
                            OnBeforeRemove(this, src.Directory);
                            await src.Directory.DeleteAsync(cancellationToken: cancellationToken);
                            OnAfterRemove(this, src.Directory);
                            OnAfterMove?.Invoke(this, (src.Directory, pastedDir, true));
                        }
                        else
                        {
                            OnBeforeMove?.Invoke(this, (src.Directory, newDest, false));
                            pastedDir = await src.Directory.MoveToAsync(newDest, dstVolume, cancellationToken: cancellationToken);
                            OnAfterMove?.Invoke(this, (src.Directory, pastedDir, false));
                        }

                        await RemoveThumbsAsync(src, cancellationToken: cancellationToken);

                        pasteResp.removed.Add(src.HashedTarget);
                    }
                    else
                    {
                        OnBeforeCopy?.Invoke(this, (src.Directory, newDest, true));
                        pastedDir = await CopyToAsync(src.Directory, newDest, dstVolume, copyOverwrite, cancellationToken: cancellationToken);
                        OnAfterCopy?.Invoke(this, (src.Directory, pastedDir, true));
                    }

                    if (pastedDir != null)
                    {
                        var hash = pastedDir.GetHash(dstPath.Volume, pathParser);
                        pasteResp.added.Add(await pastedDir.ToFileInfoAsync(hash, dstPath.HashedTarget, dstPath.Volume, connector.Options, cancellationToken: cancellationToken));
                    }
                }
                else
                {
                    IFile pastedFile;
                    var file = src.File;
                    var newDest = Path.Combine(dstPath.Directory.FullName, file.Name);
                    var newDestFile = new FileSystemFile(newDest, dstPath.Volume);
                    var exists = await newDestFile.ExistsAsync;

                    if (exists && cmd.Renames.Contains(newDestFile.Name))
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(newDestFile.Name);
                        var ext = Path.GetExtension(newDestFile.Name);
                        var backupName = $"{fileNameWithoutExt}{cmd.Suffix}{ext}";
                        var fullBakName = Path.Combine(newDestFile.Parent.FullName, backupName);
                        var bakFile = new FileSystemFile(fullBakName, newDestFile.Volume);

                        if (await bakFile.ExistsAsync)
                            backupName = await bakFile.GetCopyNameAsync(cmd.Suffix, cancellationToken: cancellationToken);

                        var prevName = newDestFile.Name;
                        OnBeforeRename?.Invoke(this, (newDestFile, backupName));
                        await newDestFile.RenameAsync(backupName, cancellationToken: cancellationToken);
                        OnAfterRename?.Invoke(this, (newDestFile, prevName));

                        pasteResp.added.Add(await newDestFile.ToFileInfoAsync(dstPath.HashedTarget, newDestFile.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
                    }

                    if (isCut)
                    {
                        pastedFile = await SafeMoveToAsync(file, dstPath.Directory.FullName,
                            dstVolume, dstVolume.CopyOverwrite, cancellationToken: cancellationToken);
                        await RemoveThumbsAsync(src, cancellationToken: cancellationToken);
                        pasteResp.removed.Add(src.HashedTarget);
                    }
                    else
                    {
                        pastedFile = await SafeCopyToAsync(file, dstPath.Directory.FullName,
                            dstVolume, dstVolume.CopyOverwrite, cancellationToken: cancellationToken);
                    }

                    pasteResp.added.Add(await pastedFile.ToFileInfoAsync(dstPath.HashedTarget, dstPath.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
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
                var dstVolume = src.Volume;

                if (src.IsDirectory)
                {
                    var newName = await src.Directory.GetCopyNameAsync(cancellationToken: cancellationToken);
                    var newDest = Path.Combine(src.Directory.Parent.FullName, newName);

                    OnBeforeCopy?.Invoke(this, (src.Directory, newDest, true));
                    var dupDir = await CopyToAsync(src.Directory, newDest, dstVolume, copyOverwrite: false, cancellationToken: cancellationToken);
                    OnAfterCopy?.Invoke(this, (src.Directory, dupDir, true));

                    var hash = dupDir.GetHash(src.Volume, pathParser);
                    var parentHash = dupDir.GetParentHash(src.Volume, pathParser);
                    dupResp.added.Add(await dupDir.ToFileInfoAsync(hash, parentHash, src.Volume, connector.Options, cancellationToken: cancellationToken));
                }
                else
                {
                    var dupFile = await SafeCopyToAsync(src.File, src.File.Parent.FullName, dstVolume,
                        copyOverwrite: false, cancellationToken: cancellationToken);

                    var parentHash = src.File.GetParentHash(src.Volume, pathParser);
                    dupResp.added.Add(await dupFile.ToFileInfoAsync(parentHash, src.Volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
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
                searchResp.files.Add(await item.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
            }

            if (cmd.Mimes.Count == 0)
            {
                foreach (var item in await targetPath.Directory.GetDirectoriesAsync(cmd.Q,
                    searchOption: SearchOption.AllDirectories, cancellationToken: cancellationToken))
                {
                    var hash = item.GetHash(volume, pathParser);
                    var parentHash = item.Parent.Equals(targetPath.Directory) ? targetPath.HashedTarget :
                        item.GetParentHash(volume, pathParser);
                    searchResp.files.Add(await item.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
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
            var newFile = new FileSystemFile(archivePath, volume);

            if (!await newFile.CanArchiveToAsync())
                throw new PermissionDeniedException();

            if (newFile.DirectoryExists())
                throw new ExistsException(newFile.Name);

            try
            {
                OnBeforeArchive?.Invoke(this, newFile);
                using (var fileStream = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                {
                    foreach (var path in cmd.TargetPaths)
                    {
                        if (!path.FileSystem.CanBeArchived()) throw new PermissionDeniedException();

                        if (path.IsDirectory)
                        {
                            await zipFileArchiver.AddDirectoryAsync(fileStream, path.Directory,
                                fromDir: string.Empty, isDownload: false, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            zipFileArchiver.CreateEntryFromFile(fileStream, path.File, path.File.Name);
                        }
                    }
                }
                OnAfterArchive?.Invoke(this, newFile);
            }
            catch (Exception e)
            {
                OnArchiveError?.Invoke(this, (e, newFile));
                throw e;
            }

            await newFile.RefreshAsync(cancellationToken);
            archiveResp.added.Add(await newFile.ToFileInfoAsync(targetPath.HashedTarget, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
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
            var parentDir = new FileSystemDirectory(fromPath, volume);
            var fromDir = parentDir;
            var makedir = cmd.MakeDir == 1;

            if (!targetPath.File.CanExtract()) throw new PermissionDeniedException();

            if (makedir)
            {
                fromPath = Path.Combine(fromPath, Path.GetFileNameWithoutExtension(targetPath.File.Name));
                fromDir = new FileSystemDirectory(fromPath, volume);

                if (!await fromDir.CanExtractToAsync())
                    throw new PermissionDeniedException();

                if (fromDir.FileExists())
                    throw new ExistsException(fromDir.Name);

                if (!await fromDir.ExistsAsync)
                {
                    OnBeforeMakeDir?.Invoke(this, fromDir);
                    await fromDir.CreateAsync(cancellationToken: cancellationToken);
                    OnAfterMakeDir?.Invoke(this, fromDir);
                }

                var hash = fromDir.GetHash(volume, pathParser);
                var parentHash = fromDir.GetParentHash(volume, pathParser);
                extractResp.added.Add(await fromDir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
            }

            OnBeforeExtract?.Invoke(this, (parentDir, fromDir, targetPath.File));
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

                        if (dir.FileExists())
                            throw new ExistsException(dir.Name);

                        if (!await dir.ExistsAsync)
                        {
                            OnBeforeMakeDir?.Invoke(this, dir);
                            await dir.CreateAsync(cancellationToken: cancellationToken);
                            OnAfterMakeDir?.Invoke(this, dir);
                        }

                        if (!makedir)
                        {
                            var parentHash = dir.GetParentHash(volume, pathParser);
                            var hash = dir.GetHash(volume, pathParser);
                            extractResp.added.Add(await dir.ToFileInfoAsync(hash, parentHash, volume, connector.Options, cancellationToken: cancellationToken));
                        }
                    }
                    else
                    {
                        var file = new FileSystemFile(fullName, volume);

                        if (!await file.CanExtractToAsync()) throw new PermissionDeniedException();

                        if (file.DirectoryExists())
                            throw new ExistsException(file.Name);

                        var entryModel = entry.ToEntry();
                        var isOverwrite = await file.ExistsAsync;

                        OnBeforeExtractFile?.Invoke(this, (entryModel, file, isOverwrite));
                        await zipFileArchiver.ExtractToAsync(entry, file, isOverwrite, cancellationToken: cancellationToken);
                        OnAfterExtractFile?.Invoke(this, (entryModel, file, isOverwrite));

                        if (!makedir)
                        {
                            await file.RefreshAsync(cancellationToken);
                            var parentHash = file.GetParentHash(volume, pathParser);
                            extractResp.added.Add(await file.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
                        }
                    }
                }
            }
            OnAfterExtract?.Invoke(this, (parentDir, fromDir, targetPath.File));

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

                    OnBeforeWriteData?.Invoke(this, (data, targetFile));
                    using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                    {
                        fileStream.Write(data, 0, data.Length);
                    }
                    OnAfterWriteData?.Invoke(this, (data, targetFile));
                }
                else
                {
                    using (var client = new HttpClient())
                    {
                        Func<Task<Stream>> openFunc = async () => await client.GetStreamAsync(cmd.Content);
                        using (var dataStream = await openFunc())
                        {
                            OnBeforeWriteStream?.Invoke(this, (openFunc, targetFile));
                            using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                            {
                                await dataStream.CopyToAsync(fileStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
                            }
                            OnAfterWriteStream?.Invoke(this, (openFunc, targetFile));
                        }
                    }
                }
            }
            else
            {
                OnBeforeWriteContent?.Invoke(this, (cmd.Content, cmd.Encoding, targetFile));
                using (var fileStream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                using (var writer = new StreamWriter(fileStream, Encoding.GetEncoding(cmd.Encoding)))
                {
                    writer.Write(cmd.Content);
                }
                OnAfterWriteContent?.Invoke(this, (cmd.Content, cmd.Encoding, targetFile));
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, pathParser);
            putResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));

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
                        await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

                        Func<Task<ImageWithMimeType>> getImageFunc = async () =>
                        {
                            using (var stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken))
                            {
                                return pictureEditor.Resize(stream, cmd.Width, cmd.Height, cmd.Quality);
                            }
                        };

                        Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

                        ImageWithMimeType image = await getImageFunc();

                        OnBeforeWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
                        }
                        OnAfterWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                    }
                    break;
                case ResizeCommand.Mode_Crop:
                    {
                        await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

                        Func<Task<ImageWithMimeType>> getImageFunc = async () =>
                        {
                            using (var stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken))
                            {
                                return pictureEditor.Crop(stream, cmd.X, cmd.Y,
                                    cmd.Width, cmd.Height, cmd.Quality);
                            }
                        };

                        Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

                        ImageWithMimeType image = await getImageFunc();

                        OnBeforeWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
                        }
                        OnAfterWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                    }
                    break;
                case ResizeCommand.Mode_Rotate:
                    {
                        await RemoveThumbsAsync(targetPath, cancellationToken: cancellationToken);

                        Func<Task<ImageWithMimeType>> getImageFunc = async () =>
                        {
                            using (var stream = await targetFile.OpenReadAsync(cancellationToken: cancellationToken))
                            {
                                return pictureEditor.Rotate(stream, cmd.Degree, cmd.Background, cmd.Quality);
                            }
                        };

                        Func<Task<Stream>> openStreamFunc = async () => (await getImageFunc()).ImageStream;

                        ImageWithMimeType image = await getImageFunc();

                        OnBeforeWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                        using (var stream = await targetFile.OpenWriteAsync(cancellationToken: cancellationToken))
                        {
                            await image.ImageStream.CopyToAsync(stream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
                        }
                        OnAfterWriteStream?.Invoke(this, (openStreamFunc, targetFile));
                    }
                    break;
                default:
                    throw new UnknownCommandException();
            }

            await targetFile.RefreshAsync(cancellationToken);
            var parentHash = targetFile.GetParentHash(volume, pathParser);
            resizeResp.changed.Add(await targetFile.ToFileInfoAsync(parentHash, volume, pathParser, pictureEditor, videoEditor, cancellationToken: cancellationToken));
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
            var newFile = new FileSystemFile(archivePath, volume);

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
                                fromDir: string.Empty, true, path.IsRoot ? volume.Name : null, cancellationToken: cancellationToken);
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
                await fileStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
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

        public virtual async Task<ImageWithMimeType> GetThumbAsync(PathInfo target, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target.IsDirectory) return null;

            var (thumb, thumbFile, mediaType) = await CreateThumbAsync(target.File, cancellationToken: cancellationToken);

            if (thumb != null)
            {
                thumb.ImageStream.Position = 0;
                return thumb;
            }

            if (thumbFile == null || !await thumbFile.ExistsAsync)
            {
                thumbnailBackgroundGenerator.TryAddToQueue(target.File, thumbFile, target.File.Volume.ThumbnailSize, true, mediaType);
                return null;
            }

            string mimeType = MimeHelper.GetMimeType(pictureEditor.ConvertThumbnailExtension(thumbFile.Extension));
            return new ImageWithMimeType(mimeType, await thumbFile.OpenReadAsync(cancellationToken: cancellationToken));
        }

        public virtual async Task<string> GenerateThumbPathAsync(IFile file, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var volume = file.Volume;
            var volumeTmbDir = volume.ThumbnailDirectory;
            var volumeSeparator = volume.DirectorySeparatorChar;

            if (volumeTmbDir == null)
                return null;

            if (file.FullName.StartsWith(volumeTmbDir + volumeSeparator))
                return file.FullName;

            volumeTmbDir = PathHelper.GetFullPathNormalized(volumeTmbDir);
            string relativePath = volume.GetRelativePath(file);
            string thumbDir = PathHelper.GetFullPathNormalized(Path.GetDirectoryName($"{volumeTmbDir}{relativePath}"));
            //string md5 = await file.GetFileMd5Async(cancellationToken);
            var ticks = (await file.LastWriteTimeUtcAsync).Ticks;
            string thumbName = $"{Path.GetFileNameWithoutExtension(file.Name)}_{ticks}{DefaultThumbExt}";
            return $"{thumbDir}{volumeSeparator}{thumbName}";
        }

        public virtual Task<string> GenerateThumbPathAsync(IDirectory directory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var volume = directory.Volume;
            var volumeTmbDir = volume.ThumbnailDirectory;
            var volumeSeparator = volume.DirectorySeparatorChar;

            if (volumeTmbDir == null)
                return Task.FromResult(default(string));

            if (directory.FullName.StartsWith(volumeTmbDir + volumeSeparator))
                return Task.FromResult(directory.FullName);

            volumeTmbDir = PathHelper.GetFullPathNormalized(volumeTmbDir);
            string relativePath = volume.GetRelativePath(directory);
            string thumbDir = volumeTmbDir + relativePath;
            return Task.FromResult(thumbDir);
        }

        public async Task<(ImageWithMimeType Thumb, IFile ThumbFile, MediaType? MediaType)> CreateThumbAsync(IFile file,
            bool verify = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MediaType? mediaType = null;

            if ((mediaType = file.CanGetThumb(pictureEditor, videoEditor, verify)) == null) return (null, null, mediaType);

            var thumbPath = await GenerateThumbPathAsync(file, cancellationToken: cancellationToken);

            if (thumbPath == null) return (null, null, mediaType);

            var volume = file.Volume;
            var thumbFile = volume.Driver.CreateFile(thumbPath, volume);

            if (!await thumbFile.ExistsAsync)
            {
                if (mediaType == MediaType.Image)
                {
                    var thumb = await thumbFile.CreateThumbAsync(
                        file, volume.ThumbnailSize, pictureEditor, cancellationToken: cancellationToken);

                    return (thumb, thumbFile, mediaType);
                }
                else if (mediaType == MediaType.Video)
                {
                    var thumb = await thumbFile.CreateThumbAsync(
                        file, volume.ThumbnailSize, videoEditor, cancellationToken: cancellationToken);

                    return (thumb, thumbFile, mediaType);
                }
            }

            return (null, thumbFile, mediaType);
        }

        public IFile CreateFile(string fullPath, IVolume volume)
        {
            return new FileSystemFile(fullPath, volume);
        }

        public IDirectory CreateDirectory(string fullPath, IVolume volume)
        {
            return new FileSystemDirectory(fullPath, volume);
        }

        protected virtual async Task<IFile> SafeCopyToAsync(IFile file, string newDir,
            IVolume destVolume, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!file.CanCopy()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, file.Name);
            IFile newFile = new FileSystemFile(newPath, destVolume);
            var isOverwrite = true;

            if (File.Exists(newPath))
            {
                if (!copyOverwrite)
                {
                    var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken: cancellationToken);
                    newPath = Path.Combine(newDir, newName);
                    isOverwrite = false;
                }
            }

            OnBeforeCopy?.Invoke(this, (file, newPath, isOverwrite));
            newFile = await file.CopyToAsync(newPath, destVolume, copyOverwrite, cancellationToken: cancellationToken);
            OnAfterCopy?.Invoke(this, (file, newFile, isOverwrite));

            return newFile;
        }

        protected virtual async Task<IFile> SafeMoveToAsync(IFile file, string newDir,
            IVolume destVolume, bool copyOverwrite = true, string suffix = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!file.CanMove()) throw new PermissionDeniedException();

            string newPath = Path.Combine(newDir, file.Name);
            IFile newFile = new FileSystemFile(newPath, destVolume);

            if (await newFile.ExistsAsync)
            {
                if (!copyOverwrite)
                {
                    var newName = await newFile.GetCopyNameAsync(suffix, cancellationToken: cancellationToken);
                    newPath = Path.Combine(newDir, newName);
                }
                else
                {
                    OnBeforeMove?.Invoke(this, (file, newPath, true));

                    OnBeforeCopy?.Invoke(this, (file, newPath, true));
                    newFile = await file.CopyToAsync(newPath, destVolume, true, cancellationToken: cancellationToken);
                    OnAfterCopy?.Invoke(this, (file, newFile, true));

                    OnBeforeRemove?.Invoke(this, file);
                    await file.DeleteAsync(cancellationToken: cancellationToken);
                    OnAfterRemove?.Invoke(this, file);

                    OnAfterMove?.Invoke(this, (file, newFile, true));

                    return newFile;
                }
            }

            OnBeforeMove?.Invoke(this, (file, newPath, false));
            newFile = await file.MoveToAsync(newPath, destVolume, cancellationToken: cancellationToken);
            OnAfterMove?.Invoke(this, (file, newFile, false));

            return newFile;
        }

        protected virtual async Task<IDirectory> CopyToAsync(IDirectory directory, string newDest,
            IVolume destVolume, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!directory.CanCopy()) throw new PermissionDeniedException();

            var destInfo = new FileSystemDirectory(newDest, destVolume);
            if (!await destInfo.CanCopyToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

            var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
            queue.Enqueue((directory, destInfo));

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                var currentDir = currentItem.Dir;
                var currentNewDest = currentItem.Dest;

                if (!await currentNewDest.ExistsAsync)
                {
                    OnBeforeMakeDir?.Invoke(this, currentNewDest);
                    await currentNewDest.CreateAsync(cancellationToken: cancellationToken);
                    OnAfterMakeDir?.Invoke(this, currentNewDest);
                }

                foreach (var dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var newDir = new FileSystemDirectory(Path.Combine(currentNewDest.FullName, dir.Name), directory.Volume);
                    queue.Enqueue((dir, newDir));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    await SafeCopyToAsync(file, currentNewDest.FullName, destVolume, copyOverwrite, suffix, cancellationToken: cancellationToken);
                }
            }

            await destInfo.RefreshAsync(cancellationToken);
            return destInfo;
        }

        protected virtual async Task<IDirectory> MergeAsync(IDirectory srcDir, string newDest,
            IVolume destVolume, bool copyOverwrite, string suffix = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destInfo = new FileSystemDirectory(newDest, destVolume);
            if (!await destInfo.CanMoveToAsync())
                throw new PermissionDeniedException();

            if (destInfo.FileExists())
                throw new ExistsException(destInfo.Name);

            var queue = new Queue<(IDirectory Dir, IDirectory Dest)>();
            queue.Enqueue((srcDir, destInfo));

            while (queue.Count > 0)
            {
                var currentItem = queue.Dequeue();
                var currentDir = currentItem.Dir;
                var currentNewDest = currentItem.Dest;

                if (!await currentNewDest.ExistsAsync)
                {
                    OnBeforeMakeDir(this, currentNewDest);
                    await currentNewDest.CreateAsync(cancellationToken: cancellationToken);
                    OnAfterMakeDir(this, currentNewDest);
                }

                foreach (var dir in await currentDir.GetDirectoriesAsync(cancellationToken: cancellationToken))
                {
                    var newDir = new FileSystemDirectory(Path.Combine(currentNewDest.FullName, dir.Name), srcDir.Volume);
                    queue.Enqueue((dir, newDir));
                }

                foreach (var file in await currentDir.GetFilesAsync(cancellationToken: cancellationToken))
                {
                    await SafeMoveToAsync(file, currentNewDest.FullName, destVolume, copyOverwrite, suffix, cancellationToken: cancellationToken);
                }
            }

            await destInfo.RefreshAsync();
            return destInfo;
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
                    list.Add(await item.ToFileInfoAsync(subHash, hash, volume, connector.Options, cancellationToken: cancellationToken));
                }
            }
            while (!volume.IsRoot(currentDir));
        }

        protected virtual async Task RemoveThumbsAsync(PathInfo path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.IsDirectory)
            {
                string thumbPath = await GenerateThumbPathAsync(path.Directory, cancellationToken: cancellationToken);
                if (!string.IsNullOrEmpty(thumbPath) && Directory.Exists(thumbPath))
                {
                    Directory.Delete(thumbPath, true);
                }
            }
            else
            {
                string thumbPath = await GenerateThumbPathAsync(path.File, cancellationToken: cancellationToken);
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
                await reader.BaseStream.CopyToAsync(memStream, StreamConstants.DefaultBufferSize, cancellationToken: cancellationToken);
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
