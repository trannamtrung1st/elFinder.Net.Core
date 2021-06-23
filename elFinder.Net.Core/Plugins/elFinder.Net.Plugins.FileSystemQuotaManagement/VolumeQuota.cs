namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public class VolumeQuota
    {
        public string VolumeId { get; set; }
        public double? MaxStorageSize { get; set; }

        public virtual double? MaxStorageSizeInKb
        {
            get { return MaxStorageSize.HasValue ? (double?)(MaxStorageSize.Value / 1024.0) : null; }
            set { MaxStorageSize = value.HasValue ? (value * 1024) : null; }
        }

        public virtual double? MaxStorageSizeInMb
        {
            get { return MaxStorageSizeInKb.HasValue ? (double?)(MaxStorageSizeInKb.Value / 1024.0) : null; }
            set { MaxStorageSizeInKb = value.HasValue ? (value * 1024) : null; }
        }

    }
}
