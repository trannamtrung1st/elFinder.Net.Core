using elFinder.Net.Core.Exceptions;
using System;

namespace elFinder.Net.Core.Models.Response
{
    public class ErrorResponse
    {
        public object error { get; set; }

        private Exception _exception;
        public Exception GetException()
        {
            return _exception;
        }

        public ErrorResponse(Exception ex)
        {
            _exception = ex;
        }

        public static class Factory
        {
            public static ErrorResponse UnknownCommand(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.UnknownCommand
                };
            }

            public static ErrorResponse ArchiveType(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.ArchiveType
                };
            }

            public static ErrorResponse Exists(ExistsException ex)
            {
                return new ErrorResponse(ex)
                {
                    error = new[] { ErrorResponse.Exists, ex.Name }
                };
            }

            public static ErrorResponse CommandRequired(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.CommandRequired
                };
            }

            public static ErrorResponse FileNotFound(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.FileNotFound
                };
            }

            public static ErrorResponse FolderNotFound(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.FolderNotFound
                };
            }

            public static ErrorResponse CommandParams(Exception ex)
            {
                if (ex is CommandParamsException pEx)
                {
                    return new ErrorResponse(ex)
                    {
                        error = new[] { ErrorResponse.CommandParams, pEx.Cmd }
                    };
                }

                throw new InvalidOperationException($"Must be {nameof(CommandParamsException)}");
            }

            public static ErrorResponse PermissionDenied(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.PermissionDenied
                };
            }

            public static ErrorResponse UploadFileSize(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.UploadFileSize
                };
            }

            public static ErrorResponse CommandNoSupport(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.CommandNoSupport
                };
            }

            public static ErrorResponse NotFile(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.NotFile
                };
            }

            public static ErrorResponse UploadFile(Exception ex, string fileName)
            {
                return new ErrorResponse(ex)
                {
                    error = new[] { ErrorResponse.UploadFile, fileName }
                };
            }

            public static ErrorResponse Unknown(Exception ex)
            {
                return new ErrorResponse(ex)
                {
                    error = ErrorResponse.Unknown
                };
            }
        }

        #region Constants
        public const string UnknownCommand = "errUnknownCmd";
        public const string ArchiveType = "errArcType";
        public const string Exists = "errExists";
        public const string CommandRequired = "errCmdReq";
        public const string FileNotFound = "errFileNotFound";
        public const string FolderNotFound = "errFolderNotFound";
        public const string CommandParams = "errCmdParams";
        public const string Unknown = "errUnknown";
        public const string PermissionDenied = "errPerm";
        public const string UploadFileSize = "errUploadFileSize";
        public const string CommandNoSupport = "errCmdNoSupport";
        public const string NotFile = "errNotFile";
        public const string UploadFile = "errUploadFile";
        #endregion
    }
}
