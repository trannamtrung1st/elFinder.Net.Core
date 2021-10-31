namespace elFinder.Net.Core
{
    public enum HttpMethod
    {
        Get = 1,
        Post = 2
    }

    public enum ResultType
    {
        Success = 1,
        Error = 2,
        File = 3,
    }

    public enum MediaType
    {
        Image = 1,
        Video = 2
    }

    public enum MimeDetectOption : byte
    {
        Auto = 0,
        Internal = 1,
    }

    public enum UploadConstraintType
    {
        Allow = 1,
        Deny = 2
    }

    public static class UploadConstants
    {
        public const string UploadConstraintAllValue = "all";
    }

    public static class ContentTypeNames
    {
        public static class Application
        {
            public const string Json = "application/json";
        }

        public static class Text
        {
            public const string Type = "text";
        }
    }

    public static class FileExtensions
    {
        public const string Zip = "zip";
    }

    public static class DownloadConsts
    {
        public const string ZipDownloadDefaultName = "download";
    }

    public static class ApiValues
    {
        public const string Version = "2.1059";
    }

    public static class WebConsts
    {
        public const char UrlSegmentSeparator = '/';

        public static class UriScheme
        {
            public const string Data = "data";
            public const string Http = "http";
            public const string Https = "https";
        }
    }
}
