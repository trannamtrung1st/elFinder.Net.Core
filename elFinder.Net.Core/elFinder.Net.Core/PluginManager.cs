using System;
using System.Collections.Generic;

namespace elFinder.Net.Core
{
    public class PluginManager
    {
        public PluginManager()
        {
            Features = new Dictionary<Type, object>();
        }

        public virtual IDictionary<Type, object> Features { get; set; }
    }
}
