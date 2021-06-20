using elFinder.Net.Core.Services.Drawing;

namespace elFinder.Net.Core
{
    public class PathInfo
    {
        public PathInfo(string path, IVolume volume, IFile file, string hashedTarget) : this(path, volume, hashedTarget, false)
        {
            File = file;
            FileSystem = file;
        }

        public PathInfo(string path, IVolume volume, IDirectory dir, string hashedTarget) : this(path, volume, hashedTarget, true)
        {
            Directory = dir;
            FileSystem = dir;
        }

        private PathInfo(string path, IVolume volume, string hashedTarget, bool isDirectory)
        {
            Path = path;
            Volume = volume;
            HashedTarget = hashedTarget;
            IsDirectory = isDirectory;
            IsRoot = Path == string.Empty;
        }

        public string Path { get; set; }

        public IVolume Volume { get; set; }

        public IFile File { get; }

        public IDirectory Directory { get; }

        public IFileSystem FileSystem { get; }

        public string HashedTarget { get; }

        public bool IsDirectory { get; }

        public bool IsRoot { get; }

        public bool CanCreateThumbnail(IPictureEditor pictureEditor)
        {
            return !string.IsNullOrEmpty(Volume.ThumbnailUrl)
                && !IsDirectory
                && pictureEditor.CanProcessFile(File.Extension);
        }

        public override bool Equals(object obj)
        {
            if (obj is PathInfo path)
                return path.HashedTarget == HashedTarget;
            return false;
        }

        public override int GetHashCode()
        {
            return HashedTarget.GetHashCode();
        }
    }
}
