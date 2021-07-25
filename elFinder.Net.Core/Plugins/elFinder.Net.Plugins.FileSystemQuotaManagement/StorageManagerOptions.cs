using System;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public class StorageManagerOptions
    {
        public static readonly TimeSpan DefaultStorageCachingLifeTime = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMinutes(5);
        public const int DefaultMaximumItems = 10000;
        public const int DefaultReservationsAfterCleanUp = 100;

        public TimeSpan StorageCachingLifeTime { get; set; } = DefaultStorageCachingLifeTime;
        public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;
        public int MaximumItems { get; set; } = DefaultMaximumItems;
        public int ReservationsAfterCleanUp { get; set; } = DefaultReservationsAfterCleanUp;
    }
}
