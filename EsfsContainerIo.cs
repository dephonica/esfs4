using System.IO;
using System.IO.Compression;

namespace EsfsLite
{
    public static class EsfsContainerIo
    {
        public static byte[] GetContainerBytes(Stream containerStream)
        {
            using (var targetStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(targetStream, CompressionLevel.Fastest))
                {
                    containerStream.Flush();
                    containerStream.Seek(0, SeekOrigin.Begin);

                    var copyBuffer = new byte[65536];

                    while (true)
                    {
                        var bytesReaded = containerStream.Read(copyBuffer, 0, copyBuffer.Length);

                        if (bytesReaded < 1)
                        {
                            break;
                        }

                        gzipStream.Write(copyBuffer, 0, bytesReaded);
                    }
                }

                return targetStream.GetBuffer();
            }
        }

        public static void WriteContainerToFile(Stream containerStream, string fileName)
        {
            using (var outFileStream = File.Open(fileName, FileMode.Create))
            {
                var containerBytes = GetContainerBytes(containerStream);
                outFileStream.Write(containerBytes, 0, containerBytes.Length);
            }
        }

        public static Stream PutStreamToContainer(Stream sourceStream)
        {
            var targetStream = new MemoryStream();

            using (var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress))
            {
                var copyBuffer = new byte[65536];

                while (true)
                {
                    var bytesReaded = gzipStream.Read(copyBuffer, 0, copyBuffer.Length);

                    if (bytesReaded < 1)
                    {
                        break;
                    }

                    targetStream.Write(copyBuffer, 0, bytesReaded);
                }
            }

            return targetStream;
        }

        public static Stream PutBytesToContainer(byte[] data, int length)
        {
            return PutStreamToContainer(new MemoryStream(data, 0, length));
        }

        public static Stream PutFileToContainer(string fileName)
        {
            using (var inFileStream = File.Open(fileName, FileMode.Open))
            {
                return PutStreamToContainer(inFileStream);
            }
        }
    }
}
