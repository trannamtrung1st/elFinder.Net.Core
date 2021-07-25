using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface ICryptographyProvider
    {
        byte[] HMACSHA256ComputeHash(string key, byte[] buffer);
        byte[] HMACSHA1ComputeHash(string key, byte[] buffer);
    }

    public class DefaultCryptographyProvider : ICryptographyProvider
    {
        protected readonly ConcurrentDictionary<string, HMACSHA256> hmac256Map;
        protected readonly ConcurrentDictionary<string, HMACSHA1> hmac1Map;

        public DefaultCryptographyProvider()
        {
            hmac256Map = new ConcurrentDictionary<string, HMACSHA256>();
            hmac1Map = new ConcurrentDictionary<string, HMACSHA1>();
        }

        public byte[] HMACSHA256ComputeHash(string key, byte[] buffer)
        {
            var hash = hmac256Map.GetOrAdd(key, _ => new HMACSHA256());
            lock (hash)
            {
                return hash.ComputeHash(buffer);
            }
        }

        public byte[] HMACSHA1ComputeHash(string key, byte[] buffer)
        {
            var hash = hmac1Map.GetOrAdd(key, _ => new HMACSHA1());
            lock (hash)
            {
                return hash.ComputeHash(buffer);
            }
        }
    }
}
