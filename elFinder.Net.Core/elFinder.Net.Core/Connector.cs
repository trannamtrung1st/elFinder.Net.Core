using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Extensions;
using elFinder.Net.Core.Helpers;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Models.Result;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IConnector
    {
        ConnectorOptions Options { get; }
        PluginManager PluginManager { get; }
        IList<IVolume> Volumes { get; set; }
        Task<ConnectorResult> ProcessAsync(ConnectorCommand cmd, CancellationTokenSource cancellationTokenSource = default);
        Task<PathInfo> ParsePathAsync(string target,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default);
        Task<IEnumerable<PathInfo>> ParsePathsAsync(IEnumerable<string> targets, CancellationToken cancellationToken = default);
        Task<ImageWithMimeType> GetThumbAsync(string target, CancellationToken cancellationToken = default);
        string AddVolume(IVolume volume);
    }

    public class Connector : IConnector
    {
        protected readonly ConnectorOptions options;
        protected readonly PluginManager pluginManager;
        protected readonly IPathParser pathParser;
        protected readonly IPictureEditor pictureEditor;
        protected readonly IConnectorManager connectorManager;

        public Connector(ConnectorOptions options, PluginManager pluginManager,
            IPathParser pathParser, IPictureEditor pictureEditor,
            IConnectorManager connectorManager)
        {
            this.options = options;
            this.pluginManager = pluginManager;
            this.pathParser = pathParser;
            this.pictureEditor = pictureEditor;
            this.connectorManager = connectorManager;
            Volumes = new List<IVolume>();
        }

        public virtual ConnectorOptions Options => options;
        public virtual IList<IVolume> Volumes { get; set; }
        public virtual PluginManager PluginManager => pluginManager;

        public virtual async Task<ConnectorResult> ProcessAsync(ConnectorCommand cmd, CancellationTokenSource cancellationTokenSource = default)
        {
            cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();

            if (cmd == null) throw new ArgumentNullException(nameof(cmd));
            if (cmd.Args == null) throw new ArgumentNullException(nameof(cmd.Args));

            var hasReqId = !string.IsNullOrEmpty(cmd.ReqId);

            if (hasReqId)
                connectorManager.AddCancellationTokenSource(cmd.ReqId, cancellationTokenSource);

            var cookies = new Dictionary<string, string>();
            var connResult = await ProcessCoreAsync(cmd, cookies, cancellationToken);
            connResult.Cookies = cookies;

            if (hasReqId)
                connectorManager.Release(cmd.ReqId);

            return connResult;
        }

        protected virtual async Task<ConnectorResult> ProcessCoreAsync(ConnectorCommand cmd, Dictionary<string, string> cookies,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cmd == null) throw new ArgumentNullException(nameof(cmd));
            if (cmd.Args == null) throw new ArgumentNullException(nameof(cmd.Args));

            ErrorResponse errResp;
            HttpStatusCode errSttCode = HttpStatusCode.OK;

            try
            {
                if (options.EnabledCommands?.Contains(cmd.Cmd) == false) throw new CommandNoSupportException();

                var args = cmd.Args;
                cmd.Cmd = args.GetValueOrDefault(ConnectorCommand.Param_Cmd);

                if (string.IsNullOrEmpty(cmd.Cmd))
                    throw new CommandRequiredException();

                switch (cmd.Cmd)
                {
                    case ConnectorCommand.Cmd_Abort:
                        {
                            var abortCmd = new AbortCommand();
                            abortCmd.Id = args.GetValueOrDefault(ConnectorCommand.Param_Id);

                            var success = await connectorManager.AbortAsync(abortCmd.Id, cancellationToken);

                            //return ConnectorResult.NoContent(new AbortResponse { success = success });
                            return ConnectorResult.Success(new AbortResponse
                            {
                                success = success
                            });
                        }
                    case ConnectorCommand.Cmd_Info:
                        {
                            var infoCmd = new InfoCommand();
                            infoCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            infoCmd.TargetPaths = await ParsePathsAsync(infoCmd.Targets, cancellationToken: cancellationToken);
                            cmd.CmdObject = infoCmd;

                            var distinctVolumes = infoCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Info);

                            var infoResp = await distinctVolumes[0].Driver.InfoAsync(infoCmd, cancellationToken);

                            return ConnectorResult.Success(infoResp);
                        }
                    case ConnectorCommand.Cmd_Open:
                        {
                            var openCmd = new OpenCommand();
                            openCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            openCmd.TargetPath = await ParsePathAsync(openCmd.Target, createIfNotExists: false, cancellationToken: cancellationToken);
                            openCmd.Mimes = options.MimeDetect == MimeDetectOption.Internal
                                ? args.GetValueOrDefault(ConnectorCommand.Param_Mimes)
                                : default;
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Init), out var init))
                                openCmd.Init = init;
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Tree), out var tree))
                                openCmd.Tree = tree;
                            var openVolume = openCmd.TargetPath?.Volume ?? Volumes.FirstOrDefault();
                            openCmd.Volume = openVolume;
                            cmd.CmdObject = openCmd;

                            if (openVolume == null) throw new FileNotFoundException();

                            var openResp = await openVolume.Driver.OpenAsync(openCmd, cancellationToken);

                            if (openCmd.Tree == 1)
                            {
                                foreach (var volume in Volumes)
                                {
                                    if (openCmd.TargetPath.IsRoot && volume == openVolume) continue;

                                    var rootVolumeDir = volume.Driver.CreateDirectory(volume.RootDirectory, volume);
                                    var hash = rootVolumeDir.GetHash(volume, pathParser);
                                    openResp.files.Add(await rootVolumeDir.ToFileInfoAsync(hash, null, volume, cancellationToken));
                                }
                            }

                            return ConnectorResult.Success(openResp);
                        }
                    case ConnectorCommand.Cmd_Archive:
                        {
                            var archiveCmd = new ArchiveCommand();
                            archiveCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            archiveCmd.TargetPath = await ParsePathAsync(archiveCmd.Target, cancellationToken: cancellationToken);
                            archiveCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name);
                            archiveCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            archiveCmd.TargetPaths = await ParsePathsAsync(archiveCmd.Targets, cancellationToken: cancellationToken);
                            archiveCmd.Type = args.GetValueOrDefault(ConnectorCommand.Param_Type);
                            cmd.CmdObject = archiveCmd;
                            var volume = archiveCmd.TargetPath.Volume;

                            if (archiveCmd.Targets.Count == 0 || archiveCmd.TargetPath?.IsDirectory != true)
                                throw new CommandParamsException(ConnectorCommand.Cmd_Archive);

                            var distinctVolumes = archiveCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1 || distinctVolumes[0].VolumeId != volume.VolumeId)
                                throw new CommandParamsException(ConnectorCommand.Cmd_Archive);

                            var archiveResp = await volume.Driver.ArchiveAsync(archiveCmd, cancellationToken);
                            return ConnectorResult.Success(archiveResp);
                        }
                    case ConnectorCommand.Cmd_Extract:
                        {
                            var extractCmd = new ExtractCommand();
                            extractCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            extractCmd.TargetPath = await ParsePathAsync(extractCmd.Target, cancellationToken: cancellationToken);
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_MakeDir), out var makeDir))
                                extractCmd.MakeDir = makeDir;
                            cmd.CmdObject = extractCmd;

                            if (extractCmd.TargetPath.IsDirectory)
                                throw new NotFileException();

                            var extractResp = await extractCmd.TargetPath.Volume.Driver.ExtractAsync(extractCmd, cancellationToken);
                            return ConnectorResult.Success(extractResp);
                        }
                    case ConnectorCommand.Cmd_Mkdir:
                        {
                            var mkdirCmd = new MkdirCommand();
                            mkdirCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            mkdirCmd.TargetPath = await ParsePathAsync(mkdirCmd.Target, cancellationToken: cancellationToken);
                            mkdirCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name);
                            mkdirCmd.Dirs = args.GetValueOrDefault(ConnectorCommand.Param_Dirs);
                            cmd.CmdObject = mkdirCmd;

                            var mkdirResp = await mkdirCmd.TargetPath.Volume.Driver.MkdirAsync(mkdirCmd, cancellationToken);
                            return ConnectorResult.Success(mkdirResp);
                        }
                    case ConnectorCommand.Cmd_Mkfile:
                        {
                            var mkfileCmd = new MkfileCommand();
                            mkfileCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            mkfileCmd.TargetPath = await ParsePathAsync(mkfileCmd.Target, cancellationToken: cancellationToken);
                            mkfileCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name);
                            cmd.CmdObject = mkfileCmd;

                            var mkFileResp = await mkfileCmd.TargetPath.Volume.Driver.MkfileAsync(mkfileCmd, cancellationToken);
                            return ConnectorResult.Success(mkFileResp);
                        }
                    case ConnectorCommand.Cmd_Parents:
                        {
                            var parentsCmd = new ParentsCommand();
                            parentsCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            parentsCmd.TargetPath = await ParsePathAsync(parentsCmd.Target, cancellationToken: cancellationToken);
                            cmd.CmdObject = parentsCmd;

                            var parentsResp = await parentsCmd.TargetPath.Volume.Driver.ParentsAsync(parentsCmd, cancellationToken);
                            return ConnectorResult.Success(parentsResp);
                        }
                    case ConnectorCommand.Cmd_Tmb:
                        {
                            var tmbCmd = new TmbCommand();
                            tmbCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            tmbCmd.TargetPaths = await ParsePathsAsync(tmbCmd.Targets, cancellationToken: cancellationToken);
                            cmd.CmdObject = tmbCmd;

                            var distinctVolumes = tmbCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Tmb);

                            var tmbResp = await distinctVolumes[0].Driver.TmbAsync(tmbCmd, cancellationToken);
                            return ConnectorResult.Success(tmbResp);
                        }
                    case ConnectorCommand.Cmd_Dim:
                        {
                            var dimCmd = new DimCommand();
                            dimCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            dimCmd.TargetPath = await ParsePathAsync(dimCmd.Target, cancellationToken: cancellationToken);
                            cmd.CmdObject = dimCmd;

                            var dimResp = await dimCmd.TargetPath.Volume.Driver.DimAsync(dimCmd, cancellationToken);
                            return ConnectorResult.Success(dimResp);
                        }
                    case ConnectorCommand.Cmd_Duplicate:
                        {
                            var dupCmd = new DuplicateCommand();
                            dupCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            dupCmd.TargetPaths = await ParsePathsAsync(dupCmd.Targets, cancellationToken);
                            cmd.CmdObject = dupCmd;

                            var distinctVolumes = dupCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Duplicate);

                            var dupResp = await distinctVolumes[0].Driver.DuplicateAsync(dupCmd, cancellationToken);
                            return ConnectorResult.Success(dupResp);
                        }
                    case ConnectorCommand.Cmd_Paste:
                        {
                            var pasteCmd = new PasteCommand();
                            pasteCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            pasteCmd.TargetPaths = await ParsePathsAsync(pasteCmd.Targets, cancellationToken: cancellationToken);
                            pasteCmd.Suffix = args.GetValueOrDefault(ConnectorCommand.Param_Suffix);
                            pasteCmd.Renames = args.GetValueOrDefault(ConnectorCommand.Param_Renames);
                            pasteCmd.Dst = args.GetValueOrDefault(ConnectorCommand.Param_Dst);
                            pasteCmd.DstPath = await ParsePathAsync(pasteCmd.Dst, cancellationToken: cancellationToken);
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Cut), out var cut))
                                pasteCmd.Cut = cut;
                            pasteCmd.Hashes = args.Where(kvp => kvp.Key.StartsWith(ConnectorCommand.Param_Hashes_Start))
                                .ToDictionary(o => o.Key, o => (string)o.Value);
                            cmd.CmdObject = pasteCmd;

                            var distinctVolumes = pasteCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Paste);

                            var pasteResp = await distinctVolumes[0].Driver.PasteAsync(pasteCmd, cancellationToken);
                            return ConnectorResult.Success(pasteResp);
                        }
                    case ConnectorCommand.Cmd_Get:
                        {
                            var getCmd = new GetCommand();
                            getCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            getCmd.TargetPath = await ParsePathAsync(getCmd.Target, cancellationToken: cancellationToken);
                            getCmd.Current = args.GetValueOrDefault(ConnectorCommand.Param_Current);
                            getCmd.CurrentPath = await ParsePathAsync(getCmd.Current, cancellationToken: cancellationToken);
                            getCmd.Conv = args.GetValueOrDefault(ConnectorCommand.Param_Conv);
                            cmd.CmdObject = getCmd;
                            var targetPath = getCmd.TargetPath;

                            if (targetPath.IsDirectory)
                                throw new NotFileException();

                            var getResp = await targetPath.Volume.Driver.GetAsync(getCmd, cancellationToken);
                            return ConnectorResult.Success(getResp);
                        }
                    case ConnectorCommand.Cmd_File:
                        {
                            var fileCmd = new FileCommand();
                            fileCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            fileCmd.TargetPath = await ParsePathAsync(fileCmd.Target, cancellationToken: cancellationToken);
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Download), out var download))
                                fileCmd.Download = download;
                            fileCmd.ReqId = args.GetValueOrDefault(ConnectorCommand.Param_ReqId);
                            fileCmd.CPath = args.GetValueOrDefault(ConnectorCommand.Param_CPath);
                            cmd.CmdObject = fileCmd;
                            var targetPath = fileCmd.TargetPath;
                            var volume = targetPath.Volume;

                            if (!string.IsNullOrEmpty(fileCmd.CPath))
                            {
                                // API >= 2.1.39
                                cookies[ConnectorResult.Cookie_Elfdl + fileCmd.ReqId] = "1";
                            }

                            if (targetPath.IsDirectory)
                                throw new NotFileException();

                            if (!await targetPath.File.ExistsAsync)
                                throw new FileNotFoundException();

                            var fileResp = await fileCmd.TargetPath.Volume.Driver.FileAsync(fileCmd, cancellationToken);
                            return ConnectorResult.File(fileResp);
                        }
                    case ConnectorCommand.Cmd_Rm:
                        {
                            var rmCmd = new RmCommand();
                            rmCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            rmCmd.TargetPaths = await ParsePathsAsync(rmCmd.Targets, cancellationToken: cancellationToken);
                            cmd.CmdObject = rmCmd;

                            var distinctVolumes = rmCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Rm);

                            // Command 'rm' with parent and children items, it means "Empty the parent folder"
                            var parents = rmCmd.TargetPaths.Select(path =>
                                path.FileSystem.Parent?.FullName).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
                            rmCmd.TargetPaths = rmCmd.TargetPaths.Where(o => !parents.Contains(o.FileSystem.FullName)).ToArray();

                            var rmResp = await distinctVolumes[0].Driver.RmAsync(rmCmd, cancellationToken);
                            return ConnectorResult.Success(rmResp);
                        }
                    case ConnectorCommand.Cmd_Ls:
                        {
                            var lsCmd = new LsCommand();
                            lsCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            lsCmd.TargetPath = await ParsePathAsync(lsCmd.Target, cancellationToken: cancellationToken);
                            lsCmd.Intersect = args.GetValueOrDefault(ConnectorCommand.Param_Intersect);
                            lsCmd.Mimes = options.MimeDetect == MimeDetectOption.Internal
                                ? args.GetValueOrDefault(ConnectorCommand.Param_Mimes)
                                : default;
                            cmd.CmdObject = lsCmd;

                            var lsResp = await lsCmd.TargetPath.Volume.Driver.LsAsync(lsCmd, cancellationToken);
                            return ConnectorResult.Success(lsResp);
                        }
                    case ConnectorCommand.Cmd_Put:
                        {
                            var putCmd = new PutCommand();
                            putCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            putCmd.TargetPath = await ParsePathAsync(putCmd.Target, cancellationToken: cancellationToken);
                            putCmd.Content = args.GetValueOrDefault(ConnectorCommand.Param_Content);
                            putCmd.Encoding = args.GetValueOrDefault(ConnectorCommand.Param_Encoding);
                            cmd.CmdObject = putCmd;
                            var targetPath = putCmd.TargetPath;

                            if (targetPath.IsDirectory)
                                throw new NotFileException();

                            var putResp = await putCmd.TargetPath.Volume.Driver.PutAsync(putCmd, cancellationToken);
                            return ConnectorResult.Success(putResp);
                        }
                    case ConnectorCommand.Cmd_Size:
                        {
                            var sizeCmd = new SizeCommand();
                            sizeCmd.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);
                            sizeCmd.TargetPaths = await ParsePathsAsync(sizeCmd.Targets, cancellationToken: cancellationToken);
                            cmd.CmdObject = sizeCmd;

                            var distinctVolumes = sizeCmd.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                            if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Size);

                            var sizeResp = await distinctVolumes[0].Driver.SizeAsync(sizeCmd, cancellationToken);
                            return ConnectorResult.Success(sizeResp);
                        }
                    case ConnectorCommand.Cmd_Rename:
                        {
                            var renameCmd = new RenameCommand();
                            renameCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            var targetPath = await ParsePathAsync(renameCmd.Target, cancellationToken: cancellationToken);
                            var volume = targetPath.Volume;
                            renameCmd.TargetPath = targetPath;
                            renameCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Name);
                            cmd.CmdObject = renameCmd;

                            var renameResp = await volume.Driver.RenameAsync(renameCmd, cancellationToken);
                            return ConnectorResult.Success(renameResp);
                        }
                    case ConnectorCommand.Cmd_Tree:
                        {
                            var treeCmd = new TreeCommand();
                            treeCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            treeCmd.TargetPath = await ParsePathAsync(treeCmd.Target, cancellationToken: cancellationToken);
                            cmd.CmdObject = treeCmd;

                            var treeResp = await treeCmd.TargetPath.Volume.Driver.TreeAsync(treeCmd, cancellationToken);
                            return ConnectorResult.Success(treeResp);
                        }
                    case ConnectorCommand.Cmd_Resize:
                        {
                            var resizeCmd = new ResizeCommand();
                            resizeCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            resizeCmd.TargetPath = await ParsePathAsync(resizeCmd.Target, cancellationToken: cancellationToken);
                            resizeCmd.Mode = args.GetValueOrDefault(ConnectorCommand.Param_Mode);
                            if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Quality), out var quality))
                                resizeCmd.Quality = quality;

                            switch (resizeCmd.Mode)
                            {
                                case ResizeCommand.Mode_Resize:
                                    {
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Width), out var width))
                                            resizeCmd.Width = width;
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Height), out var height))
                                            resizeCmd.Height = height;
                                    }
                                    break;
                                case ResizeCommand.Mode_Crop:
                                    {
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Width), out var width))
                                            resizeCmd.Width = width;
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Height), out var height))
                                            resizeCmd.Height = height;
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_X), out var x))
                                            resizeCmd.X = x;
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Y), out var y))
                                            resizeCmd.Y = y;
                                    }
                                    break;
                                case ResizeCommand.Mode_Rotate:
                                    {
                                        if (int.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Degree), out var degree))
                                            resizeCmd.Degree = degree;
                                        resizeCmd.Background = args.GetValueOrDefault(ConnectorCommand.Param_Bg);
                                    }
                                    break;
                                default:
                                    throw new UnknownCommandException();
                            }
                            cmd.CmdObject = resizeCmd;
                            var targetPath = resizeCmd.TargetPath;

                            var resizeResp = await resizeCmd.TargetPath.Volume.Driver.ResizeAsync(resizeCmd, cancellationToken);
                            return ConnectorResult.Success(resizeResp);
                        }
                    case ConnectorCommand.Cmd_Search:
                        {
                            var searchCmd = new SearchCommand();
                            searchCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            searchCmd.TargetPath = await ParsePathAsync(searchCmd.Target, cancellationToken: cancellationToken);
                            searchCmd.Q = args.GetValueOrDefault(ConnectorCommand.Param_Q);
                            searchCmd.Mimes = args.GetValueOrDefault(ConnectorCommand.Param_Mimes);
                            cmd.CmdObject = searchCmd;

                            SearchResponse finalSearchResp;
                            if (searchCmd.TargetPath != null)
                                finalSearchResp = await searchCmd.TargetPath.Volume.Driver.SearchAsync(searchCmd, cancellationToken);
                            else
                            {
                                finalSearchResp = new SearchResponse();

                                foreach (var volume in Volumes)
                                {
                                    var searchResp = await volume.Driver.SearchAsync(new SearchCommand
                                    {
                                        Mimes = searchCmd.Mimes,
                                        Q = searchCmd.Q,
                                        Target = volume.VolumeId,
                                        TargetPath = await ParsePathAsync(volume.VolumeId, cancellationToken: cancellationToken)
                                    }, cancellationToken);

                                    finalSearchResp.Concat(searchResp);
                                }
                            }

                            return ConnectorResult.Success(finalSearchResp);
                        }
                    case ConnectorCommand.Cmd_Upload:
                        {
                            var uploadCmd = new UploadCommand();
                            uploadCmd.Target = args.GetValueOrDefault(ConnectorCommand.Param_Target);
                            uploadCmd.TargetPath = await ParsePathAsync(uploadCmd.Target, cancellationToken: cancellationToken);
                            uploadCmd.Upload = cmd.Files.Where(o => o.Name == ConnectorCommand.Param_Upload);
                            uploadCmd.UploadPath = args.GetValueOrDefault(ConnectorCommand.Param_UploadPath);
                            uploadCmd.UploadPathInfos = await ParsePathsAsync(uploadCmd.UploadPath, cancellationToken);
                            uploadCmd.MTime = args.GetValueOrDefault(ConnectorCommand.Param_MTime);
                            uploadCmd.Name = args.GetValueOrDefault(ConnectorCommand.Param_Names);
                            uploadCmd.Renames = args.GetValueOrDefault(ConnectorCommand.Param_Renames);
                            uploadCmd.Suffix = args.GetValueOrDefault(ConnectorCommand.Param_Suffix);
                            uploadCmd.Hashes = args.Where(kvp => kvp.Key.StartsWith(ConnectorCommand.Param_Hashes_Start))
                                .ToDictionary(o => o.Key, o => (string)o.Value);
                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Overwrite), out var overwrite))
                                uploadCmd.Overwrite = overwrite;
                            cmd.CmdObject = uploadCmd;
                            var volume = uploadCmd.TargetPath.Volume;

                            if (volume.MaxUploadSize.HasValue)
                            {
                                if (uploadCmd.Upload.Any(file => file.Length > volume.MaxUploadSize))
                                    throw new UploadFileSizeException();
                            }

                            if (uploadCmd.UploadPathInfos.Any(path => path.Volume != volume))
                                throw new CommandParamsException(ConnectorCommand.Cmd_Upload);

                            var uploadResp = await volume.Driver.UploadAsync(uploadCmd, cancellationToken);
                            return ConnectorResult.Success(uploadResp);
                        }
                    case ConnectorCommand.Cmd_Zipdl:
                        {
                            var zipdlCommand = new ZipdlCommand();
                            zipdlCommand.Targets = args.GetValueOrDefault(ConnectorCommand.Param_Targets);

                            if (byte.TryParse(args.GetValueOrDefault(ConnectorCommand.Param_Download), out var download))
                                zipdlCommand.Download = download;

                            cmd.CmdObject = zipdlCommand;

                            if (zipdlCommand.Download != 1)
                            {
                                zipdlCommand.TargetPaths = await ParsePathsAsync(zipdlCommand.Targets, cancellationToken: cancellationToken);
                                var distinctVolumes = zipdlCommand.TargetPaths.Select(p => p.Volume).Distinct().ToArray();
                                if (distinctVolumes.Length != 1) throw new CommandParamsException(ConnectorCommand.Cmd_Zipdl);

                                var zipdl1stResp = await distinctVolumes[0].Driver.ZipdlAsync(zipdlCommand, cancellationToken);
                                return ConnectorResult.Success(zipdl1stResp);
                            }

                            var cwdPath = await ParsePathAsync(zipdlCommand.Targets.First(), cancellationToken: cancellationToken);
                            zipdlCommand.ArchiveFileKey = zipdlCommand.Targets[1];
                            zipdlCommand.DownloadFileName = zipdlCommand.Targets[2];
                            zipdlCommand.MimeType = zipdlCommand.Targets[3];
                            zipdlCommand.CwdPath = cwdPath;

                            var rawZipFile = await cwdPath.Volume.Driver.ZipdlRawAsync(zipdlCommand, cancellationToken);
                            return ConnectorResult.File(rawZipFile);
                        }
                    default: throw new UnknownCommandException();
                }
            }
            catch (Exception generalEx)
            {
                var rootCause = generalEx.GetRootCause();

                if (rootCause is ConnectorException ex)
                {
                    errResp = ex.ErrorResponse;
                    if (ex.StatusCode != null) errSttCode = ex.StatusCode.Value;
                }
                else if (rootCause is UnauthorizedAccessException uaEx)
                {
                    errResp = ErrorResponse.Factory.AccessDenied(uaEx);
                }
                else if (rootCause is IOException ioEx)
                {
                    errResp = ErrorResponse.Factory.AccessDenied(ioEx);
                }
                else if (rootCause is FileNotFoundException fnfEx)
                {
                    errResp = ErrorResponse.Factory.FileNotFound(fnfEx);
                }
                else if (rootCause is DirectoryNotFoundException dnfEx)
                {
                    errResp = ErrorResponse.Factory.FolderNotFound(dnfEx);
                }
                else if (rootCause is ArgumentException argEx)
                {
                    errResp = ErrorResponse.Factory.CommandParams(argEx, cmd.Cmd);
                }
                else
                {
                    errResp = ErrorResponse.Factory.Unknown(generalEx);
                    errSttCode = HttpStatusCode.InternalServerError;
                }
            }

            return ConnectorResult.Error(errResp, errSttCode);
        }

        public virtual async Task<PathInfo> ParsePathAsync(string target,
            bool createIfNotExists = true, bool fileByDefault = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(target)) return null;

            var underscoreIdx = target.IndexOf('_');
            var volumeId = target.Substring(0, underscoreIdx + 1);
            var pathHash = target.Substring(underscoreIdx + 1);
            var filePath = pathParser.Decode(pathHash);

            IVolume volume = Volumes.FirstOrDefault(v => v.VolumeId == volumeId);

            if (volume == null) throw new FileNotFoundException();

            return await volume.Driver.ParsePathAsync(filePath, volume, target, createIfNotExists, fileByDefault, cancellationToken);
        }

        public virtual string AddVolume(IVolume volume)
        {
            Volumes.Add(volume);

            if (volume.VolumeId is null)
                volume.VolumeId = $"{Volume.VolumePrefix}{Volumes.Count}{Volume.HashSeparator}";

            return volume.VolumeId;
        }

        public virtual async Task<ImageWithMimeType> GetThumbAsync(string target, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target != null)
            {
                var pathInfo = await ParsePathAsync(target, cancellationToken: cancellationToken);

                if (pathInfo.CanCreateThumbnail(pictureEditor))
                {
                    if (!pathInfo.File.CanGetThumb()) return null;

                    var fullName = pathInfo.File.FullName;
                    var originalFileName = fullName.Substring(0, fullName.LastIndexOf('_'));
                    var fullPath = $"{originalFileName}{pathInfo.File.Extension}";
                    var volumeThumbDir = pathInfo.Volume.ThumbnailDirectory;
                    var separatorChar = pathInfo.Volume.DirectorySeparatorChar;

                    if (volumeThumbDir != null)
                    {
                        IFile thumbFile;
                        if (pathInfo.File.FullName.StartsWith(volumeThumbDir + separatorChar))
                        {
                            thumbFile = pathInfo.File;
                        }
                        else
                        {
                            thumbFile = pathInfo.Volume.Driver.CreateFile($"{volumeThumbDir}{separatorChar}{pathInfo.Path}", pathInfo.Volume);
                        }

                        if (!await thumbFile.ExistsAsync)
                        {
                            var thumb = await thumbFile.CreateThumbAsync(
                                fullPath, pathInfo.Volume.ThumbnailSize, pictureEditor, cancellationToken: cancellationToken);
                            thumb.ImageStream.Position = 0;
                            return thumb;
                        }
                        else
                        {
                            string mimeType = MimeHelper.GetMimeType(pictureEditor.ConvertThumbnailExtension(thumbFile.Extension));
                            return new ImageWithMimeType(mimeType, await thumbFile.OpenReadAsync(cancellationToken: cancellationToken));
                        }
                    }
                    else
                    {
                        using (var original = await pathInfo.File.OpenReadAsync(cancellationToken: cancellationToken))
                        {
                            return pictureEditor.GenerateThumbnail(original, pathInfo.Volume.ThumbnailSize, true);
                        }
                    }
                }
            }

            return null;
        }

        public virtual async Task<IEnumerable<PathInfo>> ParsePathsAsync(IEnumerable<string> targets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tasks = targets.Select(async t => await ParsePathAsync(t, cancellationToken: cancellationToken));

            return await Task.WhenAll(tasks);
        }
    }
}
