using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Exceptions;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Interceptors
{
    public class DriverInterceptor : IInterceptor
    {
        private const int DefaultBufferSize = 81920;

        protected readonly PluginManager pluginManager;
        protected readonly QuotaManagementContext context;
        protected readonly IConnector connector;
        protected readonly IPathParser pathParser;
        protected readonly IPictureEditor pictureEditor;
        protected readonly IStorageManager storageManager;

        private readonly ISet<string> _registeredHandlers;

        public DriverInterceptor(PluginManager pluginManager,
            QuotaManagementContext context,
            IConnector connector,
            IPathParser pathParser,
            IPictureEditor pictureEditor,
            IStorageManager storageManager)
        {
            this.pluginManager = pluginManager;
            this.context = context;
            this.connector = connector;
            this.pathParser = pathParser;
            this.pictureEditor = pictureEditor;
            this.storageManager = storageManager;

            _registeredHandlers = new HashSet<string>();
        }

        public virtual void Intercept(IInvocation invocation)
        {
            if (!typeof(IDriver).IsAssignableFrom(invocation.TargetType))
                throw new InvalidOperationException($"Not an implementation of {nameof(IDriver)}");

            var quotaOptions = pluginManager.GetQuotaOptions();

            switch (invocation.Method.Name)
            {
                case nameof(IDriver.PasteAsync):
                    {
                        InterceptPaste(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.DuplicateAsync):
                    {
                        InterceptDuplicate(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.RmAsync):
                    {
                        InterceptRm(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.TmbAsync):
                    {
                        InterceptTmb(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.UploadAsync):
                    {
                        InterceptUpload(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.PutAsync):
                    {
                        InterceptPut(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.ResizeAsync):
                    {
                        InterceptResize(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.ArchiveAsync):
                    {
                        InterceptArchive(invocation, quotaOptions);
                        return;
                    }
                case nameof(IDriver.ExtractAsync):
                    {
                        InterceptExtract(invocation, quotaOptions);
                        return;
                    }
            }

            invocation.Proceed();
        }

        protected virtual void InterceptDuplicate(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var dupCmd = invocation.Arguments[0] as DuplicateCommand;
            var dstVolume = dupCmd.TargetPaths.Select(o => o.Volume).First();
            InterceptCopy<DuplicateResponse>(invocation, dstVolume, quotaOptions);
        }

        protected virtual void InterceptPaste(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var pasteCmd = invocation.Arguments[0] as PasteCommand;

            if (pasteCmd.Cut == 1)
            {
                var driver = invocation.InvocationTarget as IDriver;
                var fromVolume = pasteCmd.TargetPaths.Select(o => o.Volume).First();
                var dstVolume = pasteCmd.DstPath.Volume;
                var fromDirectory = driver.CreateDirectory(fromVolume.RootDirectory, fromVolume);
                var dstDirectory = driver.CreateDirectory(dstVolume.RootDirectory, dstVolume);
                var cancellationToken = (CancellationToken)invocation.Arguments.Last();

                DirectoryStorageCache fromStorageCache = null;
                DirectoryStorageCache dstStorageCache = null;
                bool proceeded = false;
                double? maximum = null;

                if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(dstVolume.VolumeId, out var volumeQuota))
                    maximum = volumeQuota.MaxStorageSize;

                if (!_registeredHandlers.Contains(nameof(InterceptPaste) + "cut"))
                {
                    _registeredHandlers.Add(nameof(InterceptPaste) + "cut");

                    Func<string, Task<long>> fromCreateFunc = (_) => fromDirectory.GetPhysicalStorageUsageAsync(cancellationToken);
                    Func<string, Task<long>> dstCreateFunc = (_) => dstDirectory.GetPhysicalStorageUsageAsync(cancellationToken);
                    long toSize = 0, decreaseSize = 0, fromSize = 0;

                    driver.OnBeforeMove += (sender, args) =>
                    {
                        if (args.FileSystem is IFile file)
                        {
                            (dstStorageCache, _) = storageManager.Lock(dstVolume.RootDirectory, dstCreateFunc);

                            fromSize = file.LengthAsync.Result;
                            toSize = fromSize;

                            if (fromVolume != dstVolume)
                                (fromStorageCache, _) = storageManager.Lock(fromVolume.RootDirectory, fromCreateFunc);
                            else
                            {
                                fromStorageCache = dstStorageCache;
                                decreaseSize = fromSize;
                            }

                            if (args.IsOverwrite == true)
                            {
                                var dest = new FileInfo(args.NewDest);
                                if (dest.Exists)
                                    toSize -= dest.Length;
                            }

                            if (dstStorageCache.Storage + toSize - decreaseSize > maximum)
                                throw new QuotaException(maximum.Value, dstStorageCache.Storage, quotaOptions);

                            proceeded = true;
                        }
                        else if (args.FileSystem is IDirectory directory && !args.IsOverwrite)
                        {
                            (dstStorageCache, _) = storageManager.Lock(dstVolume.RootDirectory, dstCreateFunc);

                            fromSize = directory.GetPhysicalStorageUsageAsync(cancellationToken).Result;
                            toSize = fromSize;

                            if (fromVolume != dstVolume)
                                (fromStorageCache, _) = storageManager.Lock(fromVolume.RootDirectory, fromCreateFunc);
                            else
                            {
                                fromStorageCache = dstStorageCache;
                                decreaseSize = fromSize;
                            }

                            if (dstStorageCache.Storage + toSize - decreaseSize > maximum)
                                throw new QuotaException(maximum.Value, dstStorageCache.Storage, quotaOptions);

                            proceeded = true;
                        }
                    };

                    driver.OnAfterMove += (sender, args) =>
                    {
                        if (dstStorageCache == null) return;

                        dstStorageCache.Storage += toSize;
                        fromStorageCache.Storage -= fromSize;

                        storageManager.Unlock(dstStorageCache);
                        if (dstStorageCache != fromStorageCache)
                            storageManager.Unlock(fromStorageCache);

                        dstStorageCache = null;
                        fromStorageCache = null;
                    };
                }

                try
                {
                    invocation.ProceedAsyncMethod<PasteResponse>();
                }
                finally
                {
                    storageManager.Unlock(dstStorageCache);
                    if (dstStorageCache != fromStorageCache)
                        storageManager.Unlock(fromStorageCache);

                    dstStorageCache = null;
                    fromStorageCache = null;

                    if (proceeded)
                    {
                        storageManager.StartSizeCalculationThread(dstDirectory);

                        if (fromVolume != dstVolume)
                            storageManager.StartSizeCalculationThread(fromDirectory);
                    }
                }
            }
            else
            {
                var dstVolume = pasteCmd.DstPath.Volume;
                InterceptCopy<PasteResponse>(invocation, dstVolume, quotaOptions);
            }
        }

        protected virtual void InterceptCopy<T>(IInvocation invocation, IVolume dstVolume, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var dstDirectory = driver.CreateDirectory(dstVolume.RootDirectory, dstVolume);
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();

            DirectoryStorageCache storageCache = null;
            bool proceeded = false;
            double? maximum = null;

            if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(dstVolume.VolumeId, out var volumeQuota))
                maximum = volumeQuota.MaxStorageSize;

            if (!_registeredHandlers.Contains(nameof(InterceptCopy)))
            {
                _registeredHandlers.Add(nameof(InterceptCopy));

                Func<string, Task<long>> createFunc = (_) => dstDirectory.GetPhysicalStorageUsageAsync(cancellationToken);
                long copySize = 0;

                driver.OnBeforeCopy += (sender, args) =>
                {
                    if (args.FileSystem is IFile file)
                    {
                        (storageCache, _) = storageManager.Lock(dstVolume.RootDirectory, createFunc);

                        copySize = file.LengthAsync.Result;
                        if (args.IsOverwrite == true)
                        {
                            var dest = new FileInfo(args.Dest);
                            if (dest.Exists)
                                copySize -= dest.Length;
                        }

                        if (storageCache.Storage + copySize > maximum)
                            throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                        proceeded = true;
                    }
                };

                driver.OnAfterCopy += (sender, args) =>
                {
                    if (storageCache == null) return;
                    storageCache.Storage += copySize;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<T>();
            }
            finally
            {
                storageManager.Unlock(storageCache);
                storageCache = null;

                if (proceeded)
                    storageManager.StartSizeCalculationThread(dstDirectory);
            }
        }

        protected virtual void InterceptRm(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var cmd = invocation.Arguments[0] as RmCommand;
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            var proceededDirs = new HashSet<IDirectory>();

            if (!_registeredHandlers.Contains(nameof(InterceptRm)))
            {
                _registeredHandlers.Add(nameof(InterceptRm));

                long? rmLength = null;

                driver.OnBeforeRemove += (sender, args) =>
                {
                    if (args is IFile file)
                    {
                        rmLength = file.LengthAsync.Result;
                    }
                    else if (args is IDirectory dir)
                    {
                        rmLength = dir.GetSizeAndCountAsync(false, _ => true, _ => true, cancellationToken: cancellationToken).Result.Size;
                    }
                };

                driver.OnAfterRemove += (sender, args) =>
                {
                    if (rmLength == 0) return;

                    var volume = args.Volume;
                    var volumeDir = driver.CreateDirectory(volume.RootDirectory, volume);
                    Func<string, Task<long>> createFunc = (_) => volumeDir.GetPhysicalStorageUsageAsync(cancellationToken);

                    var (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    try
                    {
                        storageCache.Storage -= rmLength.Value;
                        rmLength = null;
                        proceededDirs.Add(volumeDir);
                    }
                    finally
                    {
                        if (storageCache != null)
                            storageManager.Unlock(storageCache);
                        storageCache = null;
                    }
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<RmResponse>();
            }
            finally
            {
                foreach (var dir in proceededDirs)
                    storageManager.StartSizeCalculationThread(dir);
            }
        }

        protected virtual void InterceptTmb(IInvocation invocation, QuotaOptions quotaOptions)
        {
            invocation.Proceed();
        }

        protected virtual void InterceptUpload(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var cmd = invocation.Arguments[0] as UploadCommand;
            var volume = cmd.TargetPath.Volume;
            var volumeDir = driver.CreateDirectory(volume.RootDirectory, volume);
            var proceededDirs = new HashSet<IDirectory>();
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            double? maximum = null;
            DirectoryStorageCache storageCache = null;

            if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(volume.VolumeId, out var volumeQuota))
                maximum = volumeQuota.MaxStorageSize;

            if (!_registeredHandlers.Contains(nameof(InterceptUpload)))
            {
                _registeredHandlers.Add(nameof(InterceptUpload));

                Func<string, Task<long>> createFunc = (_) => volumeDir.GetPhysicalStorageUsageAsync(cancellationToken);

                long uploadLength = 0;

                driver.OnBeforeUpload += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (args.IsChunking)
                    {
                        try
                        {
                            var totalUploadLength = cmd.RangeInfo.TotalBytes;

                            if (args.IsOverwrite)
                            {
                                var destLength = args.DestFile.LengthAsync.Result;
                                totalUploadLength -= destLength;
                            }

                            if (storageCache.Storage + totalUploadLength > maximum)
                                throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                            return;
                        }
                        finally
                        {
                            storageManager.Unlock(storageCache);
                            storageCache = null;
                        }
                    }

                    uploadLength = args.FormFile.Length;
                    if (args.IsOverwrite)
                    {
                        uploadLength -= args.File.LengthAsync.Result;
                    }

                    if (storageCache.Storage + uploadLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceededDirs.Add(volumeDir);
                };

                driver.OnAfterUpload += (sender, args) =>
                {
                    if (storageCache == null && uploadLength == 0) return;
                    storageCache.Storage += uploadLength;
                    uploadLength = 0;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };

                long transferLength = 0;
                long originalLength = 0;
                long tempTransferedLength = 0;

                driver.OnBeforeChunkMerged += (sender, args) =>
                {
                    originalLength = args.IsOverwrite ? args.File.LengthAsync.Result : 0;
                };

                driver.OnBeforeChunkTransfer += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (originalLength > 0)
                    {
                        tempTransferedLength -= originalLength;
                        originalLength = 0;
                    }

                    transferLength = args.ChunkFile.LengthAsync.Result;
                    tempTransferedLength += transferLength;

                    if (storageCache.Storage + tempTransferedLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceededDirs.Add(volumeDir);
                };

                driver.OnAfterChunkTransfer += (sender, args) =>
                {
                    if (storageCache != null && tempTransferedLength > 0)
                    {
                        storageCache.Storage += tempTransferedLength;
                        tempTransferedLength = 0;
                    }

                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };

                driver.OnAfterChunkMerged += (sender, arge) =>
                {
                    if (storageCache == null)
                        (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (tempTransferedLength < 0)
                    {
                        storageCache.Storage += tempTransferedLength;
                        tempTransferedLength = 0;
                    }

                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };

                driver.OnUploadError += (sender, exception) =>
                {
                    storageManager.Unlock(storageCache);
                    storageCache = null;

                    if (exception is QuotaException)
                        throw exception;
                };

                long rmLength = 0;

                driver.OnBeforeRollbackChunk += (sender, args) =>
                {
                    if (args is IFile file)
                    {
                        rmLength = file.LengthAsync.Result;
                        proceededDirs.Add(volumeDir);
                    }
                };

                driver.OnAfterRollbackChunk += (sender, args) =>
                {
                    if (rmLength == 0 || args is IDirectory) return;

                    if (storageCache == null)
                        (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    storageCache.Storage -= rmLength;
                    rmLength = 0;
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<UploadResponse>();
            }
            finally
            {
                storageManager.Unlock(storageCache);
                storageCache = null;

                foreach (var dir in proceededDirs)
                    storageManager.StartSizeCalculationThread(dir);
            }
        }

        protected virtual void InterceptPut(IInvocation invocation, QuotaOptions quotaOptions)
        {
            InterceptWriteToTarget<PutResponse>(invocation, quotaOptions);
        }

        protected virtual void InterceptResize(IInvocation invocation, QuotaOptions quotaOptions)
        {
            InterceptWriteToTarget<ResizeResponse>(invocation, quotaOptions);
        }

        protected virtual void InterceptWriteToTarget<TResp>(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var cmd = invocation.Arguments[0] as TargetCommand;
            var volume = cmd.TargetPath.Volume;
            var volumeDir = driver.CreateDirectory(volume.RootDirectory, volume);
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            double? maximum = null;

            if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(volume.VolumeId, out var volumeQuota))
                maximum = volumeQuota.MaxStorageSize;

            DirectoryStorageCache storageCache = null;
            bool proceeded = false;

            if (!_registeredHandlers.Contains(nameof(InterceptWriteToTarget)))
            {
                _registeredHandlers.Add(nameof(InterceptWriteToTarget));

                Func<string, Task<long>> createFunc = (_) => volumeDir.GetPhysicalStorageUsageAsync(cancellationToken);

                long writeLength = 0;
                long oldLength = 0;

                driver.OnBeforeWriteData += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    writeLength = args.Data.Length;
                    if (args.File.ExistsAsync.Result)
                        oldLength = args.File.LengthAsync.Result;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                };

                driver.OnAfterWriteData += (sender, args) =>
                {
                    if (storageCache == null) return;
                    args.File.RefreshAsync(cancellationToken).Wait();
                    storageCache.Storage += args.File.LengthAsync.Result - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };

                driver.OnBeforeWriteStream += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    var memStream = new MemoryStream();
                    using (memStream)
                    {
                        using (var stream = args.OpenStreamFunc().Result)
                        {
                            stream.CopyTo(memStream, DefaultBufferSize);
                            writeLength = memStream.Length;
                        }
                    }

                    if (args.File.ExistsAsync.Result)
                        oldLength = args.File.LengthAsync.Result;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                };

                driver.OnAfterWriteStream += (sender, args) =>
                {
                    if (storageCache == null) return;
                    args.File.RefreshAsync(cancellationToken).Wait();
                    storageCache.Storage += args.File.LengthAsync.Result - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };

                driver.OnBeforeWriteContent += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    writeLength = Encoding.GetEncoding(args.Encoding).GetByteCount(args.Content + Environment.NewLine) + 1;
                    if (args.File.ExistsAsync.Result)
                        oldLength = args.File.LengthAsync.Result;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                };

                driver.OnAfterWriteContent += (sender, args) =>
                {
                    if (storageCache == null) return;
                    args.File.RefreshAsync(cancellationToken).Wait();
                    storageCache.Storage += args.File.LengthAsync.Result - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<TResp>();
            }
            finally
            {
                storageManager.Unlock(storageCache);
                storageCache = null;

                if (proceeded)
                    storageManager.StartSizeCalculationThread(volumeDir);
            }
        }

        protected virtual void InterceptArchive(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var cmd = invocation.Arguments[0] as ArchiveCommand;
            var volume = cmd.TargetPath.Volume;
            var volumeDir = driver.CreateDirectory(volume.RootDirectory, volume);
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            double? maximum = null;

            if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(volume.VolumeId, out var volumeQuota))
                maximum = volumeQuota.MaxStorageSize;

            DirectoryStorageCache storageCache = null;
            bool proceeded = false;

            if (!_registeredHandlers.Contains(nameof(InterceptArchive)))
            {
                _registeredHandlers.Add(nameof(InterceptArchive));

                Func<string, Task<long>> createFunc = (_) => volumeDir.GetPhysicalStorageUsageAsync(cancellationToken);

                driver.OnBeforeArchive += (sender, file) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (storageCache.Storage > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                };

                driver.OnAfterArchive += (sender, file) =>
                {
                    if (storageCache == null) return;
                    file.RefreshAsync(cancellationToken).Wait();
                    var archiveLength = file.LengthAsync.Result;

                    if (storageCache.Storage + archiveLength > maximum)
                    {
                        try
                        {
                            file.DeleteAsync(cancellationToken: cancellationToken).Wait();
                        }
                        finally
                        {
                            throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);
                        }
                    }

                    storageCache.Storage += archiveLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<ArchiveResponse>();
            }
            finally
            {
                storageManager.Unlock(storageCache);
                storageCache = null;

                if (proceeded)
                    storageManager.StartSizeCalculationThread(volumeDir);
            }
        }

        protected virtual void InterceptExtract(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var driver = invocation.InvocationTarget as IDriver;
            var cmd = invocation.Arguments[0] as ExtractCommand;
            var volume = cmd.TargetPath.Volume;
            var volumeDir = driver.CreateDirectory(volume.RootDirectory, volume);
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            double? maximum = null;

            if (quotaOptions.Enabled && quotaOptions.Quotas.TryGetValue(volume.VolumeId, out var volumeQuota))
                maximum = volumeQuota.MaxStorageSize;

            DirectoryStorageCache storageCache = null;
            bool proceeded = false;

            if (!_registeredHandlers.Contains(nameof(InterceptExtract)))
            {
                _registeredHandlers.Add(nameof(InterceptExtract));

                Func<string, Task<long>> createFunc = (_) => volumeDir.GetPhysicalStorageUsageAsync(cancellationToken);

                long extractLength = 0;

                driver.OnBeforeExtractFile += (sender, args) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    extractLength = args.Entry.Length;
                    if (args.IsOverwrite)
                    {
                        extractLength -= args.DestFile.LengthAsync.Result;
                    }

                    if (storageCache.Storage + extractLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                };

                driver.OnAfterExtractFile += (sender, args) =>
                {
                    if (storageCache == null) return;
                    storageCache.Storage += extractLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                };
            }

            try
            {
                invocation.ProceedAsyncMethod<ExtractResponse>();
            }
            finally
            {
                storageManager.Unlock(storageCache);
                storageCache = null;

                if (proceeded)
                    storageManager.StartSizeCalculationThread(volumeDir);
            }
        }
    }
}
