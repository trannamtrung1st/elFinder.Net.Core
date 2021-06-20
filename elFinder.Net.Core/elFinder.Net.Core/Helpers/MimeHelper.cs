using MimeTypes;

namespace elFinder.Net.Core.Helpers
{
    public static class MimeHelper
    {
        public static MimeType GetMimeType(string ext)
        {
            var mimeType = MimeTypeMap.GetMimeType(ext);
            string[] split = mimeType.Split('/');
            return new MimeType { Type = split[0], Subtype = split[1] };
        }
    }
}
