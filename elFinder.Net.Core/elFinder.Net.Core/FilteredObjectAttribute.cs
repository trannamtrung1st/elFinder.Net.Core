using System;

namespace elFinder.Net.Core
{
    public class FilteredObjectAttribute
    {
        public FilteredObjectAttribute()
        {
        }

        /// <summary>
        /// For 3rd-party connectors to process their own expressions
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// Lowest priority (after file and directory filter)
        /// </summary>
        public Predicate<IFileSystem> ObjectFilter { get; set; }

        public Predicate<IFile> FileFilter { get; set; }

        public Predicate<IDirectory> DirectoryFilter { get; set; }

        public bool? Read { get; set; }

        public bool? Write { get; set; }

        public bool? Locked { get; set; }

        public bool? Visible { get; set; }

        public bool? ShowOnly { get; set; }

        public bool? Access { get; set; }
    }
}
