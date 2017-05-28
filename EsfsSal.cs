using System;
using System.IO;

namespace EsfsLite
{
    public class EsfsSal
    {
        private readonly object _lock = new object();

        private readonly Stream _containerStream;

        private readonly byte[] _sectorData;

        public EsfsSal(Stream containerStream)
        {
            _containerStream = containerStream;

            _sectorData = new byte[Esfs.SectorSizeRawBytes];
        }

        private void UpsizeContainer(Int64 newSize)
        {
            var needBytes = newSize - _containerStream.Length;

            if (needBytes > 0)
            {
                _containerStream.Seek(0, SeekOrigin.End);

                for (var n = 0; n < Esfs.SectorSizeRawBytes; n++)
                {
                    _sectorData[n] = 0;
                }

                var bytesRemain = needBytes;

                while (bytesRemain > 0)
                {
                    var bytesToWrite = bytesRemain;
                    if (bytesToWrite > Esfs.SectorSizeRawBytes)
                    {
                        bytesToWrite = Esfs.SectorSizeRawBytes;
                    }

                    _containerStream.Write(_sectorData, 0, (int) bytesToWrite);

                    bytesRemain -= bytesToWrite;
                }
            }
        }

        public void PutSector(Int64 index, byte[] data, int offset)
        {
            if (index == 0)
            {
                throw new EsfsException("Unable to put sector data - invalid sector index (zero)");
            }

            lock (_lock)
            {
                var containerOffset = index* Esfs.SectorSizeRawBytes;
                if (containerOffset >= _containerStream.Length)
                {
                    UpsizeContainer(containerOffset + Esfs.SectorSizeRawBytes);
                }

                _containerStream.Seek(containerOffset, SeekOrigin.Begin);

                Array.Copy(data, offset, _sectorData, 0, Esfs.SectorSizeRawBytes);

                _containerStream.Write(_sectorData, 0, Esfs.SectorSizeRawBytes);
            }
        }

        public byte[] GetSector(Int64 index)
        {
            if (index == 0)
            {
                throw new EsfsException("Unable to get sector data - invalid sector index (zero)");
            }

            lock (_lock)
            {
                var containerOffset = index*Esfs.SectorSizeRawBytes;
                if (containerOffset >= _containerStream.Length)
                {
                    UpsizeContainer(containerOffset + Esfs.SectorSizeRawBytes);
                }

                _containerStream.Seek(containerOffset, SeekOrigin.Begin);

                _containerStream.Read(_sectorData, 0, Esfs.SectorSizeRawBytes);

                return _sectorData;
            }
        }

        public bool IsValidSector(Int64 index)
        {
            var containerOffset = index * Esfs.SectorSizeRawBytes;
            var isValid = (containerOffset + Esfs.SectorSizeRawBytes) < _containerStream.Length;

            return isValid;
        }
    }
}
