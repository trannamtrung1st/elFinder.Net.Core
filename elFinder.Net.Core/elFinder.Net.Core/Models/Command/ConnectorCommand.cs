using elFinder.Net.Core.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace elFinder.Net.Core.Models.Command
{
    public class ConnectorCommand
    {
        public static readonly IEnumerable<string> AllCommands;
        public static readonly IEnumerable<string> AllUiCommands;
        public static readonly IEnumerable<string> NotSupportedUICommands = new[] { Ui_callback, Ui_chmod, Ui_editor, Ui_netmount, Ui_ping };

        static ConnectorCommand()
        {
            var constFields = typeof(ConnectorCommand)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string)).ToArray();

            AllCommands = constFields.Where(fi => fi.Name.StartsWith(CommandPrefix))
                .Select(x => (string)x.GetRawConstantValue())
                .ToArray();

            AllUiCommands = constFields.Where(fi => fi.Name.StartsWith(UiCommandPrefix))
                .Select(x => (string)x.GetRawConstantValue())
                .ToArray();
        }

        public string ReqId { get; set; }
        public string Cmd { get; set; }
        public object CmdObject { get; set; }
        public HttpMethod Method { get; set; }
        public IReadOnlyDictionary<string, StringValues> RequestHeaders { get; set; }
        public IReadOnlyDictionary<string, StringValues> Query { get; set; }
        public IReadOnlyDictionary<string, StringValues> Form { get; set; }
        public IReadOnlyDictionary<string, StringValues> Args => Method == HttpMethod.Get ? Query : Form;
        public IEnumerable<IFormFileWrapper> Files { get; set; }

        #region Constants
        public const string Param_Cmd = "cmd";
        public const string Param_Target = "target";
        public const string Param_Q = "q";
        public const string Param_Mimes = "mimes[]";
        public const string Param_Init = "init";
        public const string Param_Tree = "tree";
        public const string Param_Name = "name";
        public const string Param_MakeDir = "makedir";
        public const string Param_Dirs = "dirs[]";
        public const string Param_Targets = "targets[]";
        public const string Param_Type = "type";
        public const string Param_Upload = "upload[]";
        public const string Param_UploadPath = "upload_path[]";
        public const string Param_MTime = "mtime[]";
        public const string Param_Names = "name[]";
        public const string Param_Renames = "renames[]";
        public const string Param_Suffix = "suffix";
        public const string Param_Hashes_Start = "hashes[";
        public const string Param_Overwrite = "overwrite";
        public const string Param_Intersect = "intersect[]";
        public const string Param_Content = "content";
        public const string Param_Encoding = "encoding";
        public const string Param_Download = "download";
        public const string Param_ReqId = "reqid";
        public const string Param_CPath = "cpath";
        public const string Param_Current = "current";
        public const string Param_Conv = "conv";
        public const string Param_Cut = "cut";
        public const string Param_Dst = "dst";
        public const string Param_Width = "width";
        public const string Param_Height = "height";
        public const string Param_X = "x";
        public const string Param_Y = "y";
        public const string Param_Mode = "mode";
        public const string Param_Degree = "degree";
        public const string Param_Bg = "bg";
        public const string Param_Quality = "quality";
        public const string Param_Id = "id";

        public const string Header_ReqId = "X-elFinderReqid";

        public const string CommandPrefix = "Cmd_";
        public const string Cmd_Abort = "abort";
        public const string Cmd_Archive = "archive";
        public const string Cmd_Info = "info";
        public const string Cmd_Extract = "extract";
        public const string Cmd_Open = "open";
        public const string Cmd_File = "file";
        public const string Cmd_Mkdir = "mkdir";
        public const string Cmd_Mkfile = "mkfile";
        public const string Cmd_Parents = "parents";
        public const string Cmd_Tmb = "tmb";
        public const string Cmd_Dim = "dim";
        public const string Cmd_Paste = "paste";
        public const string Cmd_Duplicate = "duplicate";
        public const string Cmd_Get = "get";
        public const string Cmd_Rm = "rm";
        public const string Cmd_Ls = "ls";
        public const string Cmd_Put = "put";
        public const string Cmd_Size = "size";
        public const string Cmd_Rename = "rename";
        public const string Cmd_Tree = "tree";
        public const string Cmd_Resize = "resize";
        public const string Cmd_Search = "search";
        public const string Cmd_Upload = "upload";
        public const string Cmd_Zipdl = "zipdl";

        public const string UiCommandPrefix = "Ui_";
        public const string Ui_editor = "editor";
        public const string Ui_ping = "ping";
        public const string Ui_callback = "callback";
        public const string Ui_archive = "archive";
        public const string Ui_back = "back";
        public const string Ui_chmod = "chmod";
        public const string Ui_colwidth = "colwidth";
        public const string Ui_copy = "copy";
        public const string Ui_cut = "cut";
        public const string Ui_download = "download";
        public const string Ui_duplicate = "duplicate";
        public const string Ui_edit = "edit";
        public const string Ui_extract = "extract";
        public const string Ui_forward = "forward";
        public const string Ui_fullscreen = "fullscreen";
        public const string Ui_getfile = "getfile";
        public const string Ui_help = "help";
        public const string Ui_home = "home";
        public const string Ui_info = "info";
        public const string Ui_mkdir = "mkdir";
        public const string Ui_mkfile = "mkfile";
        public const string Ui_netmount = "netmount";
        public const string Ui_netunmount = "netunmount";
        public const string Ui_open = "open";
        public const string Ui_opendir = "opendir";
        public const string Ui_paste = "paste";
        public const string Ui_places = "places";
        public const string Ui_quicklook = "quicklook";
        public const string Ui_reload = "reload";
        public const string Ui_rename = "rename";
        public const string Ui_resize = "resize";
        public const string Ui_restore = "restore";
        public const string Ui_rm = "rm";
        public const string Ui_search = "search";
        public const string Ui_sort = "sort";
        public const string Ui_up = "up";
        public const string Ui_upload = "upload";
        public const string Ui_view = "view";
        public const string Ui_zipdl = "zipdl";
        #endregion
    }
}
