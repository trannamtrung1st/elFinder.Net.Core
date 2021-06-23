namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public class StorageManagerOptions
    {
        public const int DefaultStorageCachingMinutes = 30;
        public const int DefaultMaximumItems = 10000;
        public const int DefaultPollingIntervalInMinutes = 5;
        public const int DefaultReservationsAfterCleanUp = 100;

        public int StorageCachingMinutes { get; set; } = DefaultStorageCachingMinutes;
        public int MaximumItems { get; set; } = DefaultMaximumItems;
        public int ReservationsAfterCleanUp { get; set; } = DefaultReservationsAfterCleanUp;
        public int PollingIntervalInMinutes { get; set; } = DefaultPollingIntervalInMinutes;
    }
}
