using System;
using System.Collections.Generic;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts
{
    public class QuotaManagementContext
    {
        public QuotaManagementContext()
        {
            Features = new Dictionary<Type, object>();
        }

        public IDictionary<Type, object> Features { get; }
    }
}
