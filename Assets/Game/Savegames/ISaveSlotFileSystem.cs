using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SantasLittleHelpers.Savegames
{
    public interface ISaveSlotFileSystem
    {
        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        IEnumerable<string> EnumerateFiles(string path, string searchPattern);

        bool FileExists(string path);

        string ReadAllText(string path);

        void WriteAllText(string path, string contents);

        void Replace(string sourceFileName, string destinationFileName);

        void Move(string sourceFileName, string destinationFileName);

        void Delete(string path);
    }

    public sealed class DefaultSaveSlotFileSystem : ISaveSlotFileSystem
    {
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            return Directory.EnumerateFiles(path, searchPattern);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public void WriteAllText(string path, string contents)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents ?? string.Empty);
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void Replace(string sourceFileName, string destinationFileName)
        {
            File.Replace(sourceFileName, destinationFileName, null);
        }

        public void Move(string sourceFileName, string destinationFileName)
        {
            File.Move(sourceFileName, destinationFileName);
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }
    }
}
