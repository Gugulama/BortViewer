using System.IO;
using System.IO.Compression;

namespace ParserNII
{
    class GZip
    {
        public static string Decompress(string filename)
        {
            using (var stream = new GZipStream(new FileStream(filename, FileMode.Open), CompressionMode.Decompress))
            {
                const int size = 4096;
                var buffer = new byte[size];
                var tmpFilename = Path.GetTempFileName();
                using (var tmpFile = new FileStream(tmpFilename, FileMode.OpenOrCreate))
                {
                    int count;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            tmpFile.Write(buffer, 0, count);
                        }
                    } while (count > 0);
                }
                return tmpFilename;
            }
        }
    }
}
