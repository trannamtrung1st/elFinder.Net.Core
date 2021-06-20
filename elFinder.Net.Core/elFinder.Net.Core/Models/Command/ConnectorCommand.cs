using elFinder.Net.Core.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace elFinder.Net.Core.Models.Command
{
    public class ConnectorCommand
    {
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
        #endregion
    }
}
