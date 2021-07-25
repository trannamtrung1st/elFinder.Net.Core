using elFinder.Net.Core;
using System;

namespace elFinder.Net.Drivers.FileSystem.Services
{
    public interface IThumbnailBackgroundGenerator : IDisposable
    {
        void TryAddToQueue(IFile file, IFile tmbFile, int size, bool keepAspectRatio, MediaType? mediaType);
    }
}