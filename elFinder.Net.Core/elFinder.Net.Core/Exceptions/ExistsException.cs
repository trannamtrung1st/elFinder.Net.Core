using System;

namespace elFinder.Net.Core.Exceptions
{
    public class ExistsException : Exception
    {
        public ExistsException(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
