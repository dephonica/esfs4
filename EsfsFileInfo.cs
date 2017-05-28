using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EsfsLite
{
    public enum EsfsFileAttributes
    {
        Normal,
        System,
        Directory
    };

    struct EsfsFileInfoPart
    {
        public EsfsFileAttributes Attribute;
        public int TimestampCreated;
        public int TimestampModifyed;
        public Int64 Size;
        public Int64 StartSector;
        public Int64 EndSector;
        public Int64 ParentDirectory;
    }

    public class EsfsFileInfo : ICloneable
    {
        private const int MaxFileNameLengthBytes = 256;

        private EsfsFileInfoPart _partInfo;
        private readonly byte[] _targetBuffer;

        public EsfsFileAttributes Attribute;
        public DateTime TimestampCreated;
        public DateTime TimestampModifyed;
        public Int64 Size;
        public Int64 StartSector;
        public Int64 EndSector;
        public Int64 ParentDirectory;
        public string FileName;
        public byte[] Meta;

        public EsfsFileInfo()
        {
            _targetBuffer = new byte[Esfs.SectorSizeContentBytes];

            Meta = new byte[Esfs.SectorSizeContentBytes - 
                   Marshal.SizeOf(typeof (EsfsFileInfoPart)) - MaxFileNameLengthBytes];
        }

        public void FromBytes(byte[] data, int offset)
        {
            var partInfoSize = Marshal.SizeOf(typeof(EsfsFileInfoPart));

            var intermediateBuffer = Marshal.AllocHGlobal(partInfoSize);

            try
            {
                Marshal.Copy(data, offset, intermediateBuffer, partInfoSize);
                _partInfo = (EsfsFileInfoPart) Marshal.PtrToStructure(intermediateBuffer, typeof (EsfsFileInfoPart));
            }
            finally
            {
                Marshal.FreeHGlobal(intermediateBuffer);
            }

            var fileNameLengthBytes = 0;
            while (data[offset + partInfoSize + fileNameLengthBytes] != 0 && fileNameLengthBytes < MaxFileNameLengthBytes)
            {
                fileNameLengthBytes++;
            }

            FileName = Encoding.UTF8.GetString(data, offset + partInfoSize, fileNameLengthBytes);

            Array.Copy(data, Esfs.SectorSizeContentBytes - Meta.Length, Meta, 0, Meta.Length);

            Attribute = _partInfo.Attribute;
            TimestampCreated = _partInfo.TimestampCreated.FromUnixTimestamp();
            TimestampModifyed = _partInfo.TimestampModifyed.FromUnixTimestamp();
            Size = _partInfo.Size;
            StartSector = _partInfo.StartSector;
            EndSector = _partInfo.EndSector;
            ParentDirectory = _partInfo.ParentDirectory;
        }

        public byte[] ToBytes()
        {
            _partInfo.Attribute = Attribute;
            _partInfo.TimestampCreated = TimestampCreated.ToUnixTimestamp();
            _partInfo.TimestampModifyed = TimestampModifyed.ToUnixTimestamp();
            _partInfo.Size = Size;
            _partInfo.StartSector = StartSector;
            _partInfo.EndSector = EndSector;
            _partInfo.ParentDirectory = ParentDirectory;

            var partInfoSize = Marshal.SizeOf(typeof(EsfsFileInfoPart));

            var intermediateBuffer = Marshal.AllocHGlobal(partInfoSize);

            try
            {
                Marshal.StructureToPtr(_partInfo, intermediateBuffer, false);
                Marshal.Copy(intermediateBuffer, _targetBuffer, 0, partInfoSize);
            }
            finally
            {
                Marshal.FreeHGlobal(intermediateBuffer);
            }

            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);

            for (var n = partInfoSize; n < _targetBuffer.Length; n++)
            {
                _targetBuffer[n] = 0;
            }

            var fileNameBytesLength = fileNameBytes.Length;
            if (fileNameBytesLength > MaxFileNameLengthBytes)
            {
                fileNameBytesLength = MaxFileNameLengthBytes;
            }

            Array.Copy(fileNameBytes, 0, _targetBuffer, partInfoSize, fileNameBytesLength);
            Array.Copy(Meta, 0, _targetBuffer, Esfs.SectorSizeContentBytes - Meta.Length, Meta.Length);

            return _targetBuffer;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
