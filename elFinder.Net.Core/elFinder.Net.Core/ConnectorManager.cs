using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, (CancellationTokenSource Source, DateTimeOffset CreatedTime)> _tokenMaps;
        private readonly IOptionsMonitor<ConnectorManagerOptions> _options;

        public ConnectorManager(IOptionsMonitor<ConnectorManagerOptions> options)
        {
            _tokenMaps = new ConcurrentDictionary<string, (CancellationTokenSource Source, DateTimeOffset CreatedTime)>();
            _options = options;
            StartWorker();
        }

        public Task<bool> AbortAsync(string reqId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (CancellationTokenSource Source, DateTimeOffset CreatedTime) token;

            if (_tokenMaps.TryRemove(reqId, out token))
            {
                token.Source.Cancel();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public void AddCancellationTokenSource(string reqId, CancellationTokenSource cancellationTokenSource)
        {
            if (_tokenMaps.Count >= _options.CurrentValue.MaximumItems) return;

            (CancellationTokenSource Source, DateTimeOffset CreatedTime) currentToken;

            if (_tokenMaps.TryRemove(reqId, out currentToken))
                currentToken.Source.Cancel();

            _tokenMaps[reqId] = (cancellationTokenSource, DateTimeOffset.UtcNow);
        }

        public bool Release(string reqId)
        {
            return _tokenMaps.TryRemove(reqId, out _);
        }

        private void StartWorker()
        {
            var expiredTimespan = TimeSpan.FromMinutes(_options.CurrentValue.TokenSourceCachingMinutes);
            var running = true;

            Thread thread = new Thread(() =>
            {
                while (running)
                {
                    var sleepMins = _options.CurrentValue.PollingIntervalInMinutes == 0 ?
                        ConnectorManagerOptions.DefaultPollingIntervalInMinutes : _options.CurrentValue.PollingIntervalInMinutes;

                    Thread.Sleep(TimeSpan.FromMinutes(sleepMins));

                    var expiredTokens = _tokenMaps.Where(token =>
                    {
                        var lifeTime = DateTimeOffset.UtcNow - token.Value.CreatedTime;
                        return lifeTime >= expiredTimespan;
                    }).ToArray();

                    foreach (var token in expiredTokens)
                    {
                        try
                        {
                            _tokenMaps.TryRemove(token.Key, out _);
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
