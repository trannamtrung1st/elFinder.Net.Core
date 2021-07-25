using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface ITempFileCleaner : IDisposable
    {
        TempFileCleanerOptions Options { get; }
        Task<bool> AddEntryAsync(TempFileEntry entry);
        Task<bool> RemovEntryAsync(string entryKey);
    }

    public class TempFileEntry
    {
        public string FullName { get; set; }
        public bool IsDirectory { get; set; }
        public DateTimeOffset Expired { get; set; }
    }

    public class TempFileCleanerOptions
    {
        public static readonly TimeSpan DefaultUnmanagedLifeTime = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMinutes(5);
        public const string DefaultStatusFile = ".status";

        public string StatusFile { get; set; } = DefaultStatusFile;
        public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;
        public IDictionary<string, TimeSpan> ScanFolders { get; set; } = new Dictionary<string, TimeSpan>();
    }

    public class DefaultTempFileCleaner : ITempFileCleaner
    {
        protected readonly ConcurrentDictionary<string, TempFileEntry> managedEntries;
        protected readonly IOptionsMonitor<TempFileCleanerOptions> options;

        private bool _disposedValue;
        private readonly CancellationTokenSource _tokenSource;

        public DefaultTempFileCleaner(IOptionsMonitor<TempFileCleanerOptions> options)
        {
            this.options = options;
            managedEntries = new ConcurrentDictionary<string, TempFileEntry>();
            _tokenSource = new CancellationTokenSource();
            StartCleanerThread();
        }

        public TempFileCleanerOptions Options => options.CurrentValue;

        public Task<bool> AddEntryAsync(TempFileEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            return Task.FromResult(managedEntries.TryAdd(entry.FullName, entry));
        }

        public Task<bool> RemovEntryAsync(string entryKey)
        {
            return Task.FromResult(managedEntries.TryRemove(entryKey, out _));
        }

        protected virtual void StartCleanerThread()
        {
            Thread thread = new Thread(() =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    Thread.Sleep(options.CurrentValue.PollingInterval);
                    _tokenSource.Token.ThrowIfCancellationRequested();

                    var expiredEntries = managedEntries.Where(entry =>
                    {
                        return DateTimeOffset.UtcNow >= entry.Value.Expired;
                    }).ToArray();

                    foreach (var entry in expiredEntries)
                    {
                        _tokenSource.Token.ThrowIfCancellationRequested();

                        try
                        {
                            managedEntries.TryRemove(entry.Key, out _);
                            var fullName = entry.Value.FullName;

                            if (entry.Value.IsDirectory)
                            {
                                if (Directory.Exists(fullName))
                                    Directory.Delete(fullName);
                            }
                            else if (File.Exists(fullName))
                            {
                                File.Delete(fullName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }

                    if (options.CurrentValue.ScanFolders == null) continue;

                    foreach (var scanFolderEntry in options.CurrentValue.ScanFolders)
                    {
                        _tokenSource.Token.ThrowIfCancellationRequested();
                        var scanFolder = scanFolderEntry.Key;

                        try
                        {
                            var notExpiredFolders = Directory.EnumerateDirectories(scanFolder, "*", SearchOption.AllDirectories)
                                .Where(f =>
                                {
                                    var statusFile = Path.Combine(f, options.CurrentValue.StatusFile);
                                    if (File.Exists(statusFile))
                                    {
                                        var lastWriteTime = File.GetLastWriteTimeUtc(statusFile);
                                        var lifeTime = DateTimeOffset.UtcNow - lastWriteTime;
                                        return lifeTime < scanFolderEntry.Value;
                                    }

                                    return false;
                                }).ToArray();

                            var expiredFiles = Directory.EnumerateFiles(scanFolder, "*", SearchOption.AllDirectories)
                                .Where(f => !managedEntries.ContainsKey(f))
                                .Where(f =>
                                {
                                    try
                                    {
                                        var fInfo = new FileInfo(f);

                                        if (notExpiredFolders.Contains(fInfo.Directory.FullName))
                                            return false;

                                        var lastAccessTime = fInfo.LastAccessTimeUtc;
                                        var lastWriteTime = fInfo.LastWriteTimeUtc;
                                        var finalTime = lastAccessTime > lastWriteTime ? lastAccessTime : lastWriteTime;
                                        var lifeTime = DateTimeOffset.UtcNow - finalTime;
                                        return lifeTime >= scanFolderEntry.Value;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(ex);
                                        return false;
                                    }
                                }).ToArray();

                            foreach (var file in expiredFiles)
                            {
                                _tokenSource.Token.ThrowIfCancellationRequested();

                                try
                                {
                                    File.Delete(file);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex);
                                }
                            }

                            var expiredFolders = Directory.EnumerateDirectories(scanFolder, "*", SearchOption.AllDirectories)
                                .Where(f => !managedEntries.ContainsKey(f))
                                .Where(f =>
                                {
                                    try
                                    {
                                        var isEmpty = Directory.EnumerateFiles(f, "*", SearchOption.AllDirectories).Count() == 0;

                                        if (isEmpty)
                                        {
                                            var dInfo = new DirectoryInfo(f);

                                            if (notExpiredFolders.Contains(dInfo.FullName))
                                                return false;

                                            var lastWriteTime = dInfo.LastWriteTimeUtc;
                                            var lifeTime = DateTimeOffset.UtcNow - lastWriteTime;
                                            return lifeTime >= scanFolderEntry.Value;
                                        }

                                        return false;
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(ex);
                                        return false;
                                    }
                                }).ToArray();

                            foreach (var dir in expiredFolders)
                            {
                                _tokenSource.Token.ThrowIfCancellationRequested();

                                try
                                {
                                    Directory.Delete(dir);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
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
        // ~DefaultTempFileCleaner()
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
}
