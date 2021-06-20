using System;

namespace elFinder.Net.Core.Helpers
{
    public static class GCHelper
    {
        public static void WaitForCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
