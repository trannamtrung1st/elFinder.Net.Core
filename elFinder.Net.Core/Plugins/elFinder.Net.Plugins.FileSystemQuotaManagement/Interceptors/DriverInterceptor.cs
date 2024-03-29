﻿using Castle.DynamicProxy;
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

                    driver.OnBeforeMove.Add(async (fileSystem, newDest, isOverwrite) =>
                    {
                        if (fileSystem is IFile file)
                        {
                            (dstStorageCache, _) = storageManager.Lock(dstVolume.RootDirectory, dstCreateFunc);

                            fromSize = await file.LengthAsync;
                            toSize = fromSize;

                            if (fromVolume != dstVolume)
                                (fromStorageCache, _) = storageManager.Lock(fromVolume.RootDirectory, fromCreateFunc);
                            else
                            {
                                fromStorageCache = dstStorageCache;
                                decreaseSize = fromSize;
                            }

                            if (isOverwrite == true)
                            {
                                var dest = new FileInfo(newDest);
                                if (dest.Exists)
                                    toSize -= dest.Length;
                            }

                            if (dstStorageCache.Storage + toSize - decreaseSize > maximum)
                                throw new QuotaException(maximum.Value, dstStorageCache.Storage, quotaOptions);

                            proceeded = true;
                        }
                        else if (fileSystem is IDirectory directory && !isOverwrite)
                        {
                            (dstStorageCache, _) = storageManager.Lock(dstVolume.RootDirectory, dstCreateFunc);

                            fromSize = await directory.GetPhysicalStorageUsageAsync(cancellationToken);
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
                    });

                    driver.OnAfterMove.Add((fileSystem, newFileSystem, isOverwrite) =>
                    {
                        if (dstStorageCache == null) return Task.CompletedTask;

                        dstStorageCache.Storage += toSize;
                        fromStorageCache.Storage -= fromSize;

                        storageManager.Unlock(dstStorageCache);
                        if (dstStorageCache != fromStorageCache)
                            storageManager.Unlock(fromStorageCache);

                        dstStorageCache = null;
                        fromStorageCache = null;

                        return Task.CompletedTask;
                    });
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

                driver.OnBeforeCopy.Add(async (fileSystem, destPath, isOverwrite) =>
                {
                    if (fileSystem is IFile file)
                    {
                        (storageCache, _) = storageManager.Lock(dstVolume.RootDirectory, createFunc);

                        copySize = await file.LengthAsync;
                        if (isOverwrite == true)
                        {
                            var dest = new FileInfo(destPath);
                            if (dest.Exists)
                                copySize -= dest.Length;
                        }

                        if (storageCache.Storage + copySize > maximum)
                            throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                        proceeded = true;
                    }
                });

                driver.OnAfterCopy.Add((fileSystem, destPath, isOverwrite) =>
                {
                    if (storageCache == null) return Task.CompletedTask;
                    storageCache.Storage += copySize;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                    return Task.CompletedTask;
                });
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

                driver.OnBeforeRemove.Add(async (IFileSystem fileSystem) =>
                {
                    if (fileSystem is IFile file)
                    {
                        rmLength = await file.LengthAsync;
                    }
                    else if (fileSystem is IDirectory dir)
                    {
                        rmLength = (await dir
                            .GetSizeAndCountAsync(false, _ => true, _ => true, cancellationToken: cancellationToken)).Size;
                    }
                });

                driver.OnAfterRemove.Add((IFileSystem fileSystem) =>
                {
                    if (rmLength == 0) return Task.CompletedTask;

                    var volume = fileSystem.Volume;
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

                    return Task.CompletedTask;
                });
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

                driver.OnBeforeUpload.Add(async (file, destFile, formFile, isOverwrite, isChunking) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (isChunking)
                    {
                        try
                        {
                            var totalUploadLength = cmd.RangeInfo.TotalBytes;

                            if (isOverwrite)
                            {
                                var destLength = await destFile.LengthAsync;
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

                    uploadLength = formFile.Length;
                    if (isOverwrite)
                    {
                        uploadLength -= await file.LengthAsync;
                    }

                    if (storageCache.Storage + uploadLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceededDirs.Add(volumeDir);
                });

                driver.OnAfterUpload.Add((file, destFile, formFile, isOverwrite, isChunking) =>
                {
                    if (storageCache == null && uploadLength == 0) return Task.CompletedTask;
                    storageCache.Storage += uploadLength;
                    uploadLength = 0;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                    return Task.CompletedTask;
                });

                long transferLength = 0;
                long originalLength = 0;
                long tempTransferedLength = 0;

                driver.OnBeforeChunkMerged.Add(async (file, isOverwrite) =>
                {
                    originalLength = isOverwrite ? await file.LengthAsync : 0;
                });

                driver.OnBeforeChunkTransfer.Add(async (chunkFile, destFile, isOverwrite) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (originalLength > 0)
                    {
                        tempTransferedLength -= originalLength;
                        originalLength = 0;
                    }

                    transferLength = await chunkFile.LengthAsync;
                    tempTransferedLength += transferLength;

                    if (storageCache.Storage + tempTransferedLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceededDirs.Add(volumeDir);
                });

                driver.OnAfterChunkTransfer.Add((chunkFile, destFile, isOverwrite) =>
                {
                    if (storageCache != null && tempTransferedLength > 0)
                    {
                        storageCache.Storage += tempTransferedLength;
                        tempTransferedLength = 0;
                    }

                    storageManager.Unlock(storageCache);
                    storageCache = null;
                    return Task.CompletedTask;
                });

                driver.OnAfterChunkMerged.Add((file, isOverwrite) =>
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

                    return Task.CompletedTask;
                });

                driver.OnUploadError.Add((Exception exception) =>
                {
                    storageManager.Unlock(storageCache);
                    storageCache = null;

                    if (exception is QuotaException)
                        throw exception;

                    return Task.CompletedTask;
                });

                long rmLength = 0;

                driver.OnBeforeRollbackChunk.Add(async (fileSystem) =>
                {
                    if (fileSystem is IFile file)
                    {
                        rmLength = await file.LengthAsync;
                        proceededDirs.Add(volumeDir);
                    }
                });

                driver.OnAfterRollbackChunk.Add((fileSystem) =>
                {
                    if (rmLength == 0 || fileSystem is IDirectory) return Task.CompletedTask;

                    if (storageCache == null)
                        (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    storageCache.Storage -= rmLength;
                    rmLength = 0;
                    return Task.CompletedTask;
                });
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

                driver.OnBeforeWriteData.Add(async (data, file) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    writeLength = data.Length;
                    if (await file.ExistsAsync)
                        oldLength = await file.LengthAsync;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                });

                driver.OnAfterWriteData.Add(async (data, file) =>
                {
                    if (storageCache == null) return;
                    await file.RefreshAsync(cancellationToken);
                    storageCache.Storage += await file.LengthAsync - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                });

                driver.OnBeforeWriteStream.Add(async (openStreamFunc, file) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    var memStream = new MemoryStream();
                    using (memStream)
                    {
                        using (var stream = await openStreamFunc())
                        {
                            stream.CopyTo(memStream, DefaultBufferSize);
                            writeLength = memStream.Length;
                        }
                    }

                    if (await file.ExistsAsync)
                        oldLength = await file.LengthAsync;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                });

                driver.OnAfterWriteStream.Add(async (openStreamFunc, file) =>
                {
                    if (storageCache == null) return;
                    await file.RefreshAsync(cancellationToken);
                    storageCache.Storage += await file.LengthAsync - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                });

                driver.OnBeforeWriteContent.Add(async (content, encoding, file) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    writeLength = Encoding.GetEncoding(encoding).GetByteCount(content + Environment.NewLine) + 1;
                    if (await file.ExistsAsync)
                        oldLength = await file.LengthAsync;

                    if (storageCache.Storage + writeLength - oldLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                });

                driver.OnAfterWriteContent.Add(async (content, encoding, file) =>
                {
                    if (storageCache == null) return;
                    await file.RefreshAsync(cancellationToken);
                    storageCache.Storage += await file.LengthAsync - oldLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                });
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

                driver.OnBeforeArchive.Add((file) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    if (storageCache.Storage > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;

                    return Task.CompletedTask;
                });

                driver.OnAfterArchive.Add(async (file) =>
                {
                    if (storageCache == null) return;
                    await file.RefreshAsync(cancellationToken);
                    var archiveLength = await file.LengthAsync;

                    if (storageCache.Storage + archiveLength > maximum)
                    {
                        try
                        {
                            await file.DeleteAsync(cancellationToken: cancellationToken);
                        }
                        finally
                        {
                            throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);
                        }
                    }

                    storageCache.Storage += archiveLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                });
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

                driver.OnBeforeExtractFile.Add(async (entry, destFile, isOverwrite) =>
                {
                    (storageCache, _) = storageManager.Lock(volume.RootDirectory, createFunc);

                    extractLength = entry.Length;
                    if (isOverwrite)
                    {
                        extractLength -= await destFile.LengthAsync;
                    }

                    if (storageCache.Storage + extractLength > maximum)
                        throw new QuotaException(maximum.Value, storageCache.Storage, quotaOptions);

                    proceeded = true;
                });

                driver.OnAfterExtractFile.Add((entry, destFile, isOverwrite) =>
                {
                    if (storageCache == null) return Task.CompletedTask;
                    storageCache.Storage += extractLength;
                    storageManager.Unlock(storageCache);
                    storageCache = null;
                    return Task.CompletedTask;
                });
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
