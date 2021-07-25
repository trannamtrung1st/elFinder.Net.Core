using elFinder.Net.Core.Models.Command;
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
        void AddCancellationTokenSource(RequestCommand cmd, CancellationTokenSource cancellationTokenSource);
        Task<(bool Success, RequestCommand Cmd)> AbortAsync(string reqId, CancellationToken cancellationToken = default);
        bool ReleaseRequest(string reqId);
        T GetLock<T>(string key, Func<string, T> lockCreate) where T : ConnectorLock;
        T GetLock<T>(string key) where T : ConnectorLock;
        bool ReleaseLockCache(string key);
    }

    public class ConnectorManagerOptions
    {
        public const int DefaultMaximumItems = 10000;
        public static readonly TimeSpan DefaultCcTokenSourceCachingLifeTime = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DefaultLockCachingLifeTime = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMinutes(5);

        public int MaximumItems { get; set; } = DefaultMaximumItems;
        public TimeSpan TokenSourceCachingLifeTime { get; set; } = DefaultCcTokenSourceCachingLifeTime;
        public TimeSpan LockCachingLifeTime { get; set; } = DefaultLockCachingLifeTime;
        public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;
    }

    public class ConnectorManager : IConnectorManager
    {
        protected readonly ConcurrentDictionary<string, (RequestCommand Cmd, CancellationTokenSource Source, DateTimeOffset CreatedTime)> tokenMaps;
        protected readonly IOptionsMonitor<ConnectorManagerOptions> options;

        private readonly ConcurrentDictionary<string, ConnectorLock> _connectorLocks;
        private bool _disposedValue;
        private readonly CancellationTokenSource _tokenSource;

        public ConnectorManager(IOptionsMonitor<ConnectorManagerOptions> options)
        {
            _connectorLocks = new ConcurrentDictionary<string, ConnectorLock>();
            tokenMaps = new ConcurrentDictionary<string, (RequestCommand Cmd, CancellationTokenSource Source, DateTimeOffset CreatedTime)>();
            this.options = options;
            _tokenSource = new CancellationTokenSource();
            StartRequestIdCleaner();
            StartLockCleaner();
        }

        public virtual Task<(bool Success, RequestCommand Cmd)> AbortAsync(string reqId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (RequestCommand Cmd, CancellationTokenSource Source, DateTimeOffset CreatedTime) token;

            if (tokenMaps.TryRemove(reqId, out token))
            {
                token.Source.Cancel();
                return Task.FromResult((true, token.Cmd));
            }

            return Task.FromResult((false, default(RequestCommand)));
        }

        public virtual void AddCancellationTokenSource(RequestCommand cmd, CancellationTokenSource cancellationTokenSource)
        {
            if (tokenMaps.Count >= options.CurrentValue.MaximumItems) return;

            if (tokenMaps.ContainsKey(cmd.ReqId))
                throw new InvalidOperationException();

            tokenMaps[cmd.ReqId] = (cmd, cancellationTokenSource, DateTimeOffset.UtcNow);
        }

        public T GetLock<T>(string key, Func<string, T> lockCreate) where T : ConnectorLock
        {
            var lockObj = _connectorLocks.GetOrAdd(key, lockCreate);
            lockObj.LastAccess = DateTimeOffset.UtcNow;
            return lockObj as T;
        }

        public T GetLock<T>(string key) where T : ConnectorLock
        {
            ConnectorLock lockObj;

            if (_connectorLocks.TryGetValue(key, out lockObj))
                lockObj.LastAccess = DateTimeOffset.UtcNow;

            return lockObj as T;
        }

        public bool ReleaseLockCache(string key)
        {
            return _connectorLocks.TryRemove(key, out _);
        }

        public virtual bool ReleaseRequest(string reqId)
        {
            return tokenMaps.TryRemove(reqId, out _);
        }

        protected virtual void StartRequestIdCleaner()
        {
            Thread thread = new Thread(() =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    Thread.Sleep(options.CurrentValue.PollingInterval);
                    _tokenSource.Token.ThrowIfCancellationRequested();

                    var expiredTokens = tokenMaps.Where(token =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - token.Value.CreatedTime;
                        return lifeTime >= options.CurrentValue.TokenSourceCachingLifeTime;
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

        protected virtual void StartLockCleaner()
        {
            Thread thread = new Thread(() =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    Thread.Sleep(options.CurrentValue.PollingInterval);
                    _tokenSource.Token.ThrowIfCancellationRequested();

                    var expiredLocks = _connectorLocks.Where(lockObj =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - lockObj.Value.LastAccess;
                        return lifeTime >= options.CurrentValue.LockCachingLifeTime;
                    }).ToArray();

                    foreach (var lockObj in expiredLocks)
                    {
                        try
                        {
                            _connectorLocks.TryRemove(lockObj.Key, out _);
                        }
                        catch (Exception) { }
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DefaultThumbnailBackgroundGenerator()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class ConnectorLock
    {
        public DateTimeOffset LastAccess { get; internal set; }

        public void Deactivate()
        {
            LastAccess = DateTimeOffset.MinValue;
        }
    }
}
