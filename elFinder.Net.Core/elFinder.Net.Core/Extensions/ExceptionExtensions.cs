using System;

namespace elFinder.Net.Core.Extensions
{
    public static class ExceptionExtensions
    {
        public static Exception GetRootCause(this Exception e)
        {
            var rootCause = e;

            while (rootCause.InnerException != null)
                rootCause = rootCause.InnerException;

            return rootCause;
        }
    }
}
