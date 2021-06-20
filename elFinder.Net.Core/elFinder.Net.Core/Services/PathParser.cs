using System;
using System.Text;

namespace elFinder.Net.Core.Services
{
    public interface IPathParser
    {
        string Decode(string hash);
        string Encode(string path);
        string Encrypt(string path);
        string Decrypt(string path);
    }

    public class PathParser : IPathParser
    {
        public string Decode(string hash)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return string.Empty;
            }

            char[] pathChars = new char[hash.Length];
            for (var i = 0; i < hash.Length; i++)
            {
                var currentChar = hash[i];
                switch (currentChar)
                {
                    case '-': pathChars[i] = '+'; break;
                    case '_': pathChars[i] = '/'; break;
                    case '.': pathChars[i] = '='; break;
                    default: pathChars[i] = currentChar; break;
                }
            }

            var decrypted = Decrypt(new string(pathChars));
            switch (decrypted.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: decrypted += "=="; break; // Two pad chars
                case 3: decrypted += "="; break; // One pad char
            }

            var base64 = Convert.FromBase64String(decrypted);
            var path = Encoding.UTF8.GetString(base64);

            return path;
        }

        public string Decrypt(string path)
        {
            // default
            return path;
        }

        public string Encode(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var encrypted = Encrypt(path);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(encrypted));

            int endPos;
            for (endPos = base64.Length - 1; endPos > 0; endPos--)
            {
                if (base64[endPos] != '=') break;
            }

            char[] pathChars = new char[endPos + 1];
            for (var i = 0; i < pathChars.Length; i++)
            {
                char currentChar = base64[i];
                switch (currentChar)
                {
                    case '+': pathChars[i] = '-'; break;
                    case '/': pathChars[i] = '_'; break;
                    case '=': pathChars[i] = '.'; break;
                    default: pathChars[i] = currentChar; break;
                }
            }

            return new string(pathChars);
        }

        public string Encrypt(string path)
        {
            // default
            return path;
        }
    }
}
