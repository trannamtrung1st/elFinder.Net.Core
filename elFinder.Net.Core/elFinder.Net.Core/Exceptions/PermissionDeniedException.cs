using System;

namespace elFinder.Net.Core.Exceptions
{
    public class PermissionDeniedException : Exception
    {
        public PermissionDeniedException() : base("")
        {
        }

        public PermissionDeniedException(string message) : base(message)
        {
        }
    }
}
