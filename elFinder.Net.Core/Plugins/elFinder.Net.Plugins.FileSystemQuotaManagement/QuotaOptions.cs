using elFinder.Net.Core.Models.Command;
using System;
using System.Collections.Generic;
using System.Linq;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement
{
    public class QuotaOptions
    {
        public static readonly IEnumerable<string> NotSupportedConnectorCommands = new string[0];

        public static readonly IEnumerable<string> NotSupportedUICommands = new[]
        {
            ConnectorCommand.Ui_restore
        };

        public QuotaOptions()
        {
            Quotas = new Dictionary<string, VolumeQuota>();
            Enabled = true;
        }

        public bool Enabled { get; set; }

        public IDictionary<string, VolumeQuota> Quotas { get; set; }

        public Func<double, StorageUnit> GetStorageUnitFunc { get; set; } = (value) =>
        {
            return StorageUnit.AllUnits.Last(o => value > o.ConvertValue);
        };
    }
}
