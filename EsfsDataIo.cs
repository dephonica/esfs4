using System;
using System.Linq;
using System.Text;

namespace EsfsLite
{
    public static class EsfsDataIo
    {
        public static void PutFloatMetaToFile(Esfs fs, string pathName, float metaFloat)
        {
            PutMetaToFile(fs, pathName, BitConverter.GetBytes(metaFloat));
        }

        public static float GetFloatMetaFromFile(Esfs fs, string pathName)
        {
            return BitConverter.ToSingle(GetMetaFromFile(fs, pathName), 0);
        }

        public static void PutStringMetaToFile(Esfs fs, string pathName, string metaString)
        {
            PutMetaToFile(fs, pathName, Encoding.UTF8.GetBytes(metaString));
        }

        public static void PutMetaToFile(Esfs fs, string pathName, byte[] metaData)
        {
            if (fs.FileExists(pathName) == false)
            {
                fs.CreateFile(pathName, EsfsFileAttributes.Normal);
            }

            var fileInstance = fs.OpenFile(pathName);
            fileInstance.SetMeta(metaData);
        }

        public static string GetStringMetaFromFile(Esfs fs, string pathName)
        {
            return Encoding.UTF8.GetString(GetMetaFromFile(fs, pathName));
        }

        public static byte[] GetMetaFromFile(Esfs fs, string pathName)
        {
            var fileInstance = fs.OpenFile(pathName);
            return fileInstance.GetMeta();
        }

        public static void PutStringToFile(Esfs fs, string pathName, string dataString)
        {
            var stringBytes = Encoding.UTF8.GetBytes(dataString);

            if (fs.FileExists(pathName) == false)
            {
                fs.CreateFile(pathName, EsfsFileAttributes.Normal);
            }

            var fileInstance = fs.OpenFile(pathName);
            fileInstance.WriteBytes(stringBytes, 0, stringBytes.Length);
        }

        public static string GetStringFromFile(Esfs fs, string pathName)
        {
            var fileInstance = fs.OpenFile(pathName);

            var fileBytes = new byte[fileInstance.Size];
            fileInstance.ReadBytes(fileBytes, 0, (int) fileInstance.Size);
            return Encoding.UTF8.GetString(fileBytes);
        }

        public static void PutFloatsToFile(Esfs fs, string pathName, float[] floats)
        {
            PutFloatsToFile(fs, pathName, floats, 0, floats.Length);
        }

        public static void PutFloatsToFile(Esfs fs, string pathName, float[] floats, int offset, int length)
        {
            var bytesBufferSize = length*sizeof (float);
            var bytesBuffer = new byte[bytesBufferSize];

            Buffer.BlockCopy(floats, offset, bytesBuffer, 0, bytesBufferSize);

            if (fs.FileExists(pathName) == false)
            {
                fs.CreateFile(pathName, EsfsFileAttributes.Normal);
            }

            fs.CreateFile(pathName, EsfsFileAttributes.Normal);

            var fileInstance = fs.OpenFile(pathName);
            fileInstance.WriteBytes(bytesBuffer, 0, bytesBufferSize);
        }

        public static float[] GetFloatsFromFile(Esfs fs, string pathName)
        {
            var fileInfo = fs.FindFile(pathName).ToArray();

            if (fileInfo.Any() == false)
            {
                throw new EsfsException(string.Format("Unable to read floats from file: No '{0}' file found", pathName));
            }

            var bufferFloats = new float[fileInfo.First().Size / sizeof(float)];
            if (GetFloatsFromFile(fs, pathName, bufferFloats, 0, bufferFloats.Length) != bufferFloats.Length)
            {
                throw new EsfsException(string.Format("Unable to read floats from file '{0}': Not enough data", pathName));
            }

            return bufferFloats;
        }

        public static int GetFloatsFromFile(Esfs fs, string pathName, float[] floatsBuffer, int offset, int length)
        {
            var fileInstance = fs.OpenFile(pathName);

            var floatsToRead = length;

            var floatsInFile = (int) fileInstance.Size/sizeof (float);
            if (floatsInFile < floatsToRead)
            {
                floatsToRead = floatsInFile;
            }

            var bytesBuffer = new byte[floatsToRead*sizeof (float)];
            fileInstance.ReadBytes(bytesBuffer, 0, bytesBuffer.Length);

            Buffer.BlockCopy(bytesBuffer, 0, floatsBuffer, 0, bytesBuffer.Length);

            return floatsToRead;
        }
    }
}
