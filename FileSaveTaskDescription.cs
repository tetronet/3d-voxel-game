using System.IO;

namespace MetaTSUK
{
    public class FileSaveTaskDescription
    {
        public string Filename;
        public byte[] Data;

        public FileSaveTaskDescription(byte[] data, string name)
        {
            Filename = name;
            Data = data;
        }
        public void Save()
        {
            File.WriteAllBytes(Filename, Data);
        }
    }
}
