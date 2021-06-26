using System;
using System.Collections.Generic;
using System.Globalization;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public struct StorageUnit
    {
        public const int ConvertUnit = 1024;

        public string DisplayName { get; set; }
        public double ConvertValue { get; set; }
        public string Format { get; set; }
        public CultureInfo CultureInfo { get; set; }

        public static StorageUnit Byte = new StorageUnit() { ConvertValue = 1, DisplayName = "byte(s)", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static StorageUnit KB = new StorageUnit() { ConvertValue = Math.Pow(ConvertUnit, 1), DisplayName = "KB", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static StorageUnit MB = new StorageUnit() { ConvertValue = Math.Pow(ConvertUnit, 2), DisplayName = "MB", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static StorageUnit GB = new StorageUnit() { ConvertValue = Math.Pow(ConvertUnit, 3), DisplayName = "GB", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static StorageUnit TB = new StorageUnit() { ConvertValue = Math.Pow(ConvertUnit, 4), DisplayName = "TB", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static StorageUnit PB = new StorageUnit() { ConvertValue = Math.Pow(ConvertUnit, 5), DisplayName = "PB", Format = "#,0.00", CultureInfo = CultureInfo.InvariantCulture };
        public static IEnumerable<StorageUnit> AllUnits = new[] { Byte, KB, MB, GB, TB, PB };
    }
}
