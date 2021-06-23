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
        public virtual string Expression { get; set; }

        /// <summary>
        /// Lowest priority (after file and directory filter)
        /// </summary>
        public virtual Predicate<IFileSystem> ObjectFilter { get; set; }

        public virtual Predicate<IFile> FileFilter { get; set; }

        public virtual Predicate<IDirectory> DirectoryFilter { get; set; }

        public virtual bool? Read { get; set; }

        public virtual bool? Write { get; set; }

        public virtual bool? Locked { get; set; }

        public virtual bool? Visible { get; set; }

        public virtual bool? ShowOnly { get; set; }

        public virtual bool? Access { get; set; }
    }
}
