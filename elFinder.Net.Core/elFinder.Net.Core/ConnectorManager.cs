using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Core
{
    public interface IConnectorManager
    {
        void AddCancellationTokenSource(string reqId, CancellationTokenSource cancellationTokenSource);
        Task<bool> AbortAsync(string reqId, CancellationToken cancellationToken = default);
        bool Release(string reqId);
        void LockDirectoryAndProceed(string dir, Func<Task> action);
    }

    public class ConnectorManagerOptions
    {
        public const int DefaultCcTokenSourceCachingMinutes = 30;
        public const int DefaultMaximumItems = 10000;
        public const int DefaultPollingIntervalInMinutes = 5;

        public int TokenSourceCachingMinutes { get; set; } = DefaultCcTokenSourceCachingMinutes;
        public int MaximumItems { get; set; } = DefaultMaximumItems;
        public int PollingIntervalInMinutes { get; set; } = DefaultPollingIntervalInMinutes;
    }

    public class ConnectorManager : IConnectorManager
    {
        protected readonly ConcurrentDictionary<string, object> directoryLocks;
        protected readonly ConcurrentDictionary<string, long> directoryLockStatuses;
        protected readonly ConcurrentDictionary<string, (CancellationTokenSource Source, DateTimeOffset CreatedTime)> tokenMaps;
        protected readonly IOptionsMonitor<ConnectorManagerOptions> options;

        public ConnectorManager(IOptionsMonitor<ConnectorManagerOptions> options)
        {
            directoryLocks = new ConcurrentDictionary<string, object>();
            directoryLockStatuses = new ConcurrentDictionary<string, long>();
            tokenMaps = new ConcurrentDictionary<string, (CancellationTokenSource Source, DateTimeOffset CreatedTime)>();
            this.options = options;
            StartRequestIdCleaner();
        }

        public virtual Task<bool> AbortAsync(string reqId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (CancellationTokenSource Source, DateTimeOffset CreatedTime) token;

            if (tokenMaps.TryRemove(reqId, out token))
            {
                token.Source.Cancel();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public virtual void AddCancellationTokenSource(string reqId, CancellationTokenSource cancellationTokenSource)
        {
            if (tokenMaps.Count >= options.CurrentValue.MaximumItems) return;

            (CancellationTokenSource Source, DateTimeOffset CreatedTime) currentToken;

            if (tokenMaps.TryRemove(reqId, out currentToken))
                currentToken.Source.Cancel();

            tokenMaps[reqId] = (cancellationTokenSource, DateTimeOffset.UtcNow);
        }

        public virtual bool Release(string reqId)
        {
            return tokenMaps.TryRemove(reqId, out _);
        }

        #region Directory lock
        public void LockDirectoryAndProceed(string dir, Func<Task> action)
        {
            directoryLockStatuses.GetOrAdd(dir, 0);
            directoryLockStatuses[dir]++;
            var volumeLock = directoryLocks.GetOrAdd(dir, (key) => new object());

            lock (volumeLock)
            {
                directoryLockStatuses[dir]--;

                try
                {
                    action().Wait();
                }
                finally
                {
                    if (directoryLockStatuses[dir] == 0)
                    {
                        directoryLockStatuses.TryRemove(dir, out _);
                        directoryLocks.TryRemove(dir, out _);
                    }
                }
            }
        }
        #endregion

        protected virtual void StartRequestIdCleaner()
        {
            var expiredTimespan = TimeSpan.FromMinutes(options.CurrentValue.TokenSourceCachingMinutes);
            var running = true;

            Thread thread = new Thread(() =>
            {
                while (running)
                {
                    var sleepMins = options.CurrentValue.PollingIntervalInMinutes == 0 ?
                        ConnectorManagerOptions.DefaultPollingIntervalInMinutes : options.CurrentValue.PollingIntervalInMinutes;

                    Thread.Sleep(TimeSpan.FromMinutes(sleepMins));

                    var expiredTokens = tokenMaps.Where(token =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - token.Value.CreatedTime;
                        return lifeTime >= expiredTimespan;
                    }).ToArray();

                    foreach (var token in expiredTokens)
                    {
                        try
                        {
                            tokenMaps.TryRemove(token.Key, out _);
                            token.Value.Source.Cancel();
                        }
                        catch (Exception) { }
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }
    }
}
