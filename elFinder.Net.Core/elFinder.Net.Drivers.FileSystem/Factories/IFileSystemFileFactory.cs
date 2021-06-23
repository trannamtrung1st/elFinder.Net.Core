using elFinder.Net.Core;
using System.IO;

namespace elFinder.Net.Drivers.FileSystem.Factories
{
    public interface IFileSystemFileFactory
    {
        FileSystemFile Create(string filePath, IVolume volume, IFileSystemDirectoryFactory directoryFactory);
        FileSystemFile Create(FileInfo fileInfo, IVolume volume, IFileSystemDirectoryFactory directoryFactory);
    }

    public class FileSystemFileFactory : IFileSystemFileFactory
    {
        public FileSystemFile Create(string filePath, IVolume volume, IFileSystemDirectoryFactory directoryFactory)
        {
            return new FileSystemFile(filePath, volume, this, directoryFactory);
        }

        public FileSystemFile Create(FileInfo fileInfo, IVolume volume, IFileSystemDirectoryFactory directoryFactory)
        {
            return new FileSystemFile(fileInfo, volume, this, directoryFactory);
        }
    }
}
