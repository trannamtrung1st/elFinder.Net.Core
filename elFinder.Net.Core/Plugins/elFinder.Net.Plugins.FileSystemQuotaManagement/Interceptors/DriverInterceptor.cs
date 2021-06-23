using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Services;
using elFinder.Net.Core.Services.Drawing;
using elFinder.Net.Drivers.FileSystem.Factories;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Exceptions;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Interceptors
{
    public class DriverInterceptor : IInterceptor
    {
        protected readonly PluginManager pluginManager;
        protected readonly QuotaManagementContext context;
        protected readonly IConnector connector;
        protected readonly IPathParser pathParser;
        protected readonly IPictureEditor pictureEditor;
        protected readonly IFileSystemDirectoryFactory directoryFactory;
        protected readonly IFileSystemFileFactory fileFactory;
        protected readonly IStorageManager storageManager;

        public DriverInterceptor(PluginManager pluginManager,
            QuotaManagementContext context,
            IConnector connector,
            IPathParser pathParser,
            IPictureEditor pictureEditor,
            IFileSystemFileFactory fileFactory,
            IFileSystemDirectoryFactory directoryFactory,
            IStorageManager storageManager)
        {
            this.pluginManager = pluginManager;
            this.context = context;
            this.connector = connector;
            this.pathParser = pathParser;
            this.pictureEditor = pictureEditor;
            this.fileFactory = fileFactory;
            this.directoryFactory = directoryFactory;
            this.storageManager = storageManager;
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
            }

            invocation.Proceed();
        }

        protected virtual void InterceptPaste(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var pasteCmd = invocation.Arguments[0] as PasteCommand;

            if (pasteCmd.Cut != 1) throw new CommandNoSupportException();

            var fromVolume = pasteCmd.TargetPaths.Select(o => o.Volume).First();
            var dstVolume = pasteCmd.DstPath.Volume;
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();

            if (fromVolume != dstVolume && quotaOptions.Quotas.TryGetValue(dstVolume.VolumeId, out var volumeQuota))
            {
                var maximum = volumeQuota.MaxStorageSize ?? 0;
                var fromDirectory = directoryFactory.Create(fromVolume.RootDirectory, fromVolume, fileFactory);
                var dstDirectory = directoryFactory.Create(dstVolume.RootDirectory, dstVolume, fileFactory);
                Func<string, Task<long>> createFunc = async (_) =>
                {
                    var dirSizeAndCount = await dstDirectory.GetSizeAndCountAsync(visibleOnly: false, cancellationToken);
                    return dirSizeAndCount.Size;
                };
                Exception exception = null;
                bool proceeded = false;

                try
                {
                    storageManager.LockDirectoryStorage(dstVolume.RootDirectory, async (cache, _) =>
                    {
                        var totalSize = (await Task.WhenAll(pasteCmd.TargetPaths.Select(async o =>
                        {
                            if (o.IsDirectory) return (await o.Directory.GetSizeAndCountAsync()).Size;

                            return await o.File.LengthAsync;
                        }))).Sum();

                        if (cache.Storage + totalSize > maximum)
                            throw new QuotaException(maximum, cache.Storage, quotaOptions);
                        else
                        {
                            proceeded = true;
                            invocation.ProceedAsyncMethod<PasteResponse>();
                        };
                    }, createFunc);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (proceeded)
                {
                    storageManager.StartSizeCalculationThread(fromDirectory);
                    storageManager.StartSizeCalculationThread(dstDirectory);
                }

                if (exception != null) throw exception;

                return;
            }

            invocation.Proceed();
        }

        protected virtual void InterceptRm(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var rmCmd = invocation.Arguments[0] as RmCommand;
            var volume = rmCmd.TargetPaths.Select(o => o.Volume).First();
            var volumeDir = directoryFactory.Create(volume.RootDirectory, volume, fileFactory);
            Exception exception = null;

            try
            {
                invocation.ProceedAsyncMethod<RmResponse>();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            storageManager.StartSizeCalculationThread(volumeDir);

            if (exception != null) throw exception;
        }

        protected virtual void InterceptTmb(IInvocation invocation, QuotaOptions quotaOptions)
        {
            invocation.Proceed();
        }

        protected virtual void InterceptUpload(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var uploadData = invocation.Arguments[0] as UploadData;
            var initData = invocation.Arguments[1] as InitUploadData;
            var volume = initData.Volume;
            var volumeDir = directoryFactory.Create(volume.RootDirectory, volume, fileFactory);
            var cancellationToken = (CancellationToken)invocation.Arguments.Last();
            Exception exception = null;

            if (quotaOptions.Quotas.TryGetValue(volume.VolumeId, out var volumeQuota))
            {
                if (!context.Features.TryGetValue(typeof(UploadContext), out var uploadContextObj))
                    throw new InvalidOperationException("No context found");

                var uploadContext = uploadContextObj as UploadContext;
                var maximum = volumeQuota.MaxStorageSize ?? 0;
                Func<string, Task<long>> createFunc = async (_) =>
                {
                    var dirSizeAndCount = await volumeDir.GetSizeAndCountAsync(visibleOnly: false, cancellationToken);
                    return dirSizeAndCount.Size;
                };

                storageManager.LockDirectoryStorage(volume.RootDirectory, async (cache, _) =>
                {
                    var uploadLength = uploadData.FormFile.Length;

                    if (!uploadData.IsOverwrite)
                    {
                        if (cache.Storage + uploadLength > maximum)
                            throw new QuotaException(maximum, cache.Storage, quotaOptions);
                    }
                    else
                    {
                        uploadLength -= await uploadData.Destination.LengthAsync;
                    }

                    if (cache.Storage + uploadLength > maximum)
                        throw new QuotaException(maximum, cache.Storage, quotaOptions);

                    uploadContext.ProceededDirectories.GetOrAdd(volume.VolumeId, volumeDir);

                    invocation.ProceedAsyncMethod();

                    cache.Storage += uploadLength;
                }, createFunc);

                return;
            }

            try
            {
                invocation.Proceed();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            storageManager.StartSizeCalculationThread(volumeDir);

            if (exception != null) throw exception;
        }

    }
}
