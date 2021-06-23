namespace elFinder.Net.Core
{
    public class ObjectAttribute
    {
        public static readonly ObjectAttribute Default = new ObjectAttribute();

        public ObjectAttribute()
        {
            Locked = false; Read = true; Write = true; Visible = true; ShowOnly = false; Access = true;
        }

        public ObjectAttribute(ObjectAttribute clonedFrom)
        {
            Read = clonedFrom.Read;
            Write = clonedFrom.Write;
            Locked = clonedFrom.Locked;
            Visible = clonedFrom.Visible;
            ShowOnly = clonedFrom.ShowOnly;
            Access = clonedFrom.Access;
        }

        private bool _read;
        public virtual bool Read { get => Access ? _read : false; set => _read = value; }

        private bool _write;
        public virtual bool Write { get => Access ? _write : false; set => _write = value; }

        /// <summary>
        /// Gets or sets a list of root subfolders that should be locked (user can't remove, rename).
        /// </summary>
        private bool _locked;
        public virtual bool Locked { get => Access ? _locked : true; set => _locked = value; }

        public virtual bool Access { get; set; }

        /// <summary>
        /// Should be returned in listing results or not.
        /// </summary>
        public virtual bool Visible { get; set; }

        /// <summary>
        /// Can be download or not.
        /// </summary>
        public virtual bool ShowOnly { get; set; }
    }
}
