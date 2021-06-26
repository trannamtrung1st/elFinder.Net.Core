using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public class DirectoryStorageCache : SemaphoreSlim
    {
        public DirectoryStorageCache() : base(1, 1)
        {
            LastAccessTime = DateTimeOffset.UtcNow;
        }

        private long _storage;
        public long Storage
        {
            get => _storage; set
            {
                _storage = value;
                LastAccessTime = DateTimeOffset.UtcNow;
            }
        }

        public DateTimeOffset? LastAccessTime { get; set; }
    }

    public interface IStorageManager
    {
        (long Storage, bool IsInit) GetOrCreateDirectoryStorage(string dir, Func<string, Task<long>> createFunc);
        bool RemoveDirectoryStorage(string dir);
        Task Lock(string dir, Func<DirectoryStorageCache, bool, Task> updateFunc, Func<string, Task<long>> createFunc);
        (DirectoryStorageCache StorageCache, bool IsInit) Lock(string dir, Func<string, Task<long>> createFunc);
        void Unlock(DirectoryStorageCache storageCache);
    }

    public class StorageManager : IStorageManager
    {
        protected readonly ConcurrentDictionary<string, DirectoryStorageCache> directoryStorageCaches;
        protected readonly IOptionsMonitor<StorageManagerOptions> options;

        public StorageManager(IOptionsMonitor<StorageManagerOptions> options)
        {
            this.options = options;
            directoryStorageCaches = new ConcurrentDictionary<string, DirectoryStorageCache>();
            StartCachesCleaner();
        }

        public (long Storage, bool IsInit) GetOrCreateDirectoryStorage(string dir, Func<string, Task<long>> createFunc)
        {
            var result = GetOrCreate(dir, createFunc);

            CheckAndClearCaches();

            return (result.StorageObj.Storage, result.isInit);
        }

        public async Task Lock(string dir, Func<DirectoryStorageCache, bool, Task> updateFunc, Func<string, Task<long>> createFunc)
        {
            while (true)
            {
                var result = GetOrCreate(dir, createFunc);
                var storageObj = result.StorageObj;

                CheckAndClearCaches();

                try
                {
                    storageObj.Wait();

                    if (storageObj.LastAccessTime == null) continue;

                    await updateFunc(storageObj, result.isInit);

                    return;
                }
                finally
                {
                    if (storageObj?.CurrentCount == 0)
                        storageObj.Release();
                }
            }
        }

        public (DirectoryStorageCache StorageCache, bool IsInit) Lock(string dir, Func<string, Task<long>> createFunc)
        {
            while (true)
            {
                var result = GetOrCreate(dir, createFunc);
                var storageObj = result.StorageObj;

                CheckAndClearCaches();

                storageObj.Wait();
                if (storageObj.LastAccessTime == null)
                {
                    storageObj.Release();
                    continue;
                }

                return (storageObj, result.isInit);
            }
        }

        public void Unlock(DirectoryStorageCache storageCache)
        {
            if (storageCache?.CurrentCount == 0)
                storageCache.Release();
        }

        public bool RemoveDirectoryStorage(string dir)
        {
            if (directoryStorageCaches.TryRemove(dir, out var cache))
            {
                lock (cache)
                {
                    cache.LastAccessTime = null;
                }

                return true;
            }

            return false;
        }

        protected virtual (DirectoryStorageCache StorageObj, bool isInit) GetOrCreate(string dir, Func<string, Task<long>> createFunc)
        {
            var isInit = false;
            return (directoryStorageCaches.GetOrAdd(dir, (key) =>
            {
                isInit = true;
                return new DirectoryStorageCache
                {
                    Storage = createFunc(key).Result,
                };
            }), isInit);
        }

        protected virtual void CheckAndClearCaches()
        {
            var exceedingAmount = directoryStorageCaches.Count - options.CurrentValue.MaximumItems;
            if (exceedingAmount > 0)
            {
                lock (directoryStorageCaches)
                {
                    if (directoryStorageCaches.Count <= options.CurrentValue.MaximumItems) return;

                    var almostExpiredCaches = directoryStorageCaches.OrderByDescending(cache =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - cache.Value.LastAccessTime;
                        return lifeTime;
                    }).Take(exceedingAmount).ToArray();

                    foreach (var cache in almostExpiredCaches)
                    {
                        RemoveDirectoryStorage(cache.Key);
                    }
                }
            }
        }

        protected virtual void StartCachesCleaner()
        {
            var expiredTimespan = TimeSpan.FromMinutes(options.CurrentValue.StorageCachingMinutes);
            var running = true;

            Thread thread = new Thread(() =>
            {
                while (running)
                {
                    var sleepMins = options.CurrentValue.PollingIntervalInMinutes == 0 ?
                        StorageManagerOptions.DefaultPollingIntervalInMinutes : options.CurrentValue.PollingIntervalInMinutes;

                    Thread.Sleep(TimeSpan.FromMinutes(sleepMins));

                    var expiredCaches = directoryStorageCaches.Where(cache =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - cache.Value.LastAccessTime;
                        return lifeTime >= expiredTimespan;
                    }).ToArray();

                    foreach (var cache in expiredCaches)
                    {
                        RemoveDirectoryStorage(cache.Key);
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }
    }
}
