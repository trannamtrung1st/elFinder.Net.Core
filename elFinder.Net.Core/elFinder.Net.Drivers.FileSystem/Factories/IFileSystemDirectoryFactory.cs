using elFinder.Net.Core;
using System.IO;

namespace elFinder.Net.Drivers.FileSystem.Factories
{
    public interface IFileSystemDirectoryFactory
    {
        FileSystemDirectory Create(string dirName, IVolume volume, IFileSystemFileFactory fileFactory);
        FileSystemDirectory Create(DirectoryInfo directoryInfo, IVolume volume, IFileSystemFileFactory fileFactory);
    }

    public class FileSystemDirectoryFactory : IFileSystemDirectoryFactory
    {
        public FileSystemDirectory Create(string dirName, IVolume volume, IFileSystemFileFactory fileFactory)
        {
            return new FileSystemDirectory(dirName, volume, fileFactory, this);
        }

        public FileSystemDirectory Create(DirectoryInfo directoryInfo, IVolume volume, IFileSystemFileFactory fileFactory)
        {
            return new FileSystemDirectory(directoryInfo, volume, fileFactory, this);
        }
    }
}
