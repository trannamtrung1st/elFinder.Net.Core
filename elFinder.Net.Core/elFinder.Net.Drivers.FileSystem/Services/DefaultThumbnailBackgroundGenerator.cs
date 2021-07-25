using elFinder.Net.Core;
using elFinder.Net.Core.Services.Drawing;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public class DefaultThumbnailBackgroundGenerator : IThumbnailBackgroundGenerator
    {
        private readonly IPictureEditor _pictureEditor;
        private readonly IVideoEditor _videoEditor;

        private readonly Thread _videoWorker;
        private readonly ConcurrentQueue<string> _videoQueue;
        private readonly ConcurrentDictionary<string, (IFile File, IFile ThumbFile, int Size, bool KeepAspectRatio)> _videoMaps;
        private readonly ManualResetEventSlim _videoSignal;

        private readonly Thread _imageWorker;
        private readonly ConcurrentDictionary<string, (IFile File, IFile ThumbFile, int Size, bool KeepAspectRatio)> _imageMaps;
        private readonly ConcurrentQueue<string> _imageQueue;
        private readonly ManualResetEventSlim _imageSignal;

        private bool _disposedValue;
        private readonly CancellationTokenSource _tokenSource;

        public DefaultThumbnailBackgroundGenerator(IPictureEditor pictureEditor,
            IVideoEditor videoEditor)
        {
            _pictureEditor = pictureEditor;
            _videoEditor = videoEditor;
            _imageSignal = new ManualResetEventSlim(false);
            _videoSignal = new ManualResetEventSlim(false);
            _imageMaps = new ConcurrentDictionary<string, (IFile File, IFile ThumbFile, int Size, bool KeepAspectRatio)>();
            _videoMaps = new ConcurrentDictionary<string, (IFile File, IFile ThumbFile, int Size, bool KeepAspectRatio)>();
            _imageQueue = new ConcurrentQueue<string>();
            _videoQueue = new ConcurrentQueue<string>();
            _tokenSource = new CancellationTokenSource();
            _videoWorker = new Thread(async () => await RunVideoGeneratorAsync());
            _videoWorker.IsBackground = true;
            _videoWorker.Start();
            _imageWorker = new Thread(async () => await RunImageGeneratorAsync());
            _imageWorker.IsBackground = true;
            _imageWorker.Start();
        }

        public void TryAddToQueue(IFile file, IFile tmbFile, int size, bool keepAspectRatio, MediaType? mediaType)
        {
            if (mediaType == MediaType.Image)
            {
                _imageMaps.GetOrAdd(file.FullName, (fName) =>
                {
                    _imageQueue.Enqueue(fName);
                    return (file, tmbFile, size, keepAspectRatio);
                });
                _imageSignal.Set();
                return;
            }

            if (mediaType == MediaType.Video)
            {
                _videoMaps.GetOrAdd(file.FullName, (fName) =>
                {
                    _videoQueue.Enqueue(fName);
                    return (file, tmbFile, size, keepAspectRatio);
                });
                _videoSignal.Set();
                return;
            }
        }

        private async Task RunVideoGeneratorAsync()
        {
            while (!_tokenSource.IsCancellationRequested)
            {
                _tokenSource.Token.ThrowIfCancellationRequested();
                _videoSignal.Wait();

                string nextFile;
                while (_videoQueue.TryDequeue(out nextFile))
                {
                    _videoMaps.TryRemove(nextFile, out var tuple);
                    var (file, tmbFile, size, keepAspectRatio) = tuple;

                    try
                    {
                        await file.RefreshAsync(_tokenSource.Token);

                        if (!await file.ExistsAsync || await tmbFile.ExistsAsync) continue;

                        var thumb = await _videoEditor.GenerateThumbnailInBackgroundAsync(file, size, keepAspectRatio, cancellationToken: _tokenSource.Token);

                        if (thumb != null)
                        {
                            using (thumb)
                            {
                                thumb.ImageStream.Position = 0;
                                await tmbFile.OverwriteAsync(thumb.ImageStream, verify: false, cancellationToken: _tokenSource.Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }

                _videoMaps.Clear();

                _videoSignal.Reset();
            }
        }

        private async Task RunImageGeneratorAsync()
        {
            while (!_tokenSource.IsCancellationRequested)
            {
                _tokenSource.Token.ThrowIfCancellationRequested();
                _imageSignal.Wait();

                string nextFile;
                while (_imageQueue.TryDequeue(out nextFile))
                {
                    _imageMaps.TryRemove(nextFile, out var tuple);
                    var (file, tmbFile, size, keepAspectRatio) = tuple;

                    try
                    {
                        await file.RefreshAsync(_tokenSource.Token);

                        if (!await file.ExistsAsync || await tmbFile.ExistsAsync) continue;

                        ImageWithMimeType thumb;
                        using (var fileStream = await file.OpenReadAsync(verify: false, cancellationToken: _tokenSource.Token))
                        {
                            thumb = _pictureEditor.GenerateThumbnail(fileStream, size, keepAspectRatio);
                        }

                        if (thumb != null)
                        {
                            using (thumb)
                            {
                                thumb.ImageStream.Position = 0;
                                await tmbFile.OverwriteAsync(thumb.ImageStream, verify: false, cancellationToken: _tokenSource.Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }

                _imageMaps.Clear();

                _imageSignal.Reset();
            }
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
}
