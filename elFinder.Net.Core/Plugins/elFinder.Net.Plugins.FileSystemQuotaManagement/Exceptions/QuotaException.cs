using elFinder.Net.Core.Exceptions;
using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Exceptions
{
    public class QuotaException : ConnectorException
    {
        public const string ErrorKey = "errQuota";

        public QuotaException(double maximum, double usage, QuotaOptions quotaOptions)
        {
            Maximum = maximum;
            Usage = usage;
            string maximumStr = $"{maximum} byte(s)";
            string usageStr = $"{usage} byte(s)";

            if (quotaOptions.GetStorageUnitFunc != null)
            {
                var maxUnit = quotaOptions.GetStorageUnitFunc(maximum);
                var usageUnit = quotaOptions.GetStorageUnitFunc(usage);
                var maxConvert = (maximum / maxUnit.ConvertValue).ToString(maxUnit.Format, maxUnit.CultureInfo);
                var usageConvert = (usage / usageUnit.ConvertValue).ToString(usageUnit.Format, maxUnit.CultureInfo);
                maximumStr = $"{maxConvert} {maxUnit.DisplayName}";
                usageStr = $"{usageConvert} {usageUnit.DisplayName}";
            }

            ErrorResponse = new ErrorResponse(this)
            {
                error = new[] { ErrorKey, $"{maximumStr}", $"{usageStr}" }
            };
        }

        public double Maximum { get; set; }
        public double Usage { get; set; }
    }
}
