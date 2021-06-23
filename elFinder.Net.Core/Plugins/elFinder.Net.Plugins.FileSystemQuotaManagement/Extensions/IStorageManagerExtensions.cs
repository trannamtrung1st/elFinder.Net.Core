using elFinder.Net.Core;
using System;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions
{
    public static class IStorageManagerExtensions
    {
        public static void StartSizeCalculationThread(this IStorageManager storageManager, IDirectory directory)
        {
            try
            {
                Task.Run(() =>
                {
                    try
                    {
                        storageManager.LockDirectoryStorage(directory.FullName, async (cache, isInit) =>
                        {
                            if (isInit) return;

                            await directory.RefreshAsync();
                            var currentSize = (await directory.GetSizeAndCountAsync()).Size;
                            cache.Storage = currentSize;

                        }, async (dirFullName) =>
                        {
                            await directory.RefreshAsync();
                            var currentSize = (await directory.GetSizeAndCountAsync()).Size;
                            return currentSize;
                        });
                    }
                    catch (Exception ex)
                    {
                        storageManager.RemoveDirectoryStorage(directory.FullName);
                        throw ex;
                    }
                });
            }
            catch (Exception ex)
            {
                storageManager.RemoveDirectoryStorage(directory.FullName);
                throw ex;
            }
        }
    }
}
