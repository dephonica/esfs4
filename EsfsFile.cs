using System;
using System.IO;

namespace EsfsLite
{
    public class EsfsFile
    {
        private const int AllocationChunkSectors = 8;

        private readonly EsfsFreespace _freespace;
        private readonly EsfsFileInfo _fileInfo;

        private Int64 _pointer;

        private readonly EsfsChain _chain;

        public EsfsFile(EsfsSal salMount, EsfsFileInfo fileInfo, EsfsFreespace freespace)
        {
            _fileInfo = fileInfo;
            _freespace = freespace;

            _pointer = 0;

            _chain = new EsfsChain(salMount, fileInfo.StartSector);
        }

        public Int64 Size => _fileInfo.Size;

        public byte[] GetMeta()
        {
            return _fileInfo.Meta;
        }

        public void SetMeta(byte[] metaData)
        {
            Array.Copy(metaData, _fileInfo.Meta, Math.Min(metaData.Length, _fileInfo.Meta.Length));
            StoreFileInfo();
        }

        public void Seek(Int64 pointer, SeekOrigin seekOrigin)
        {
            if (seekOrigin == SeekOrigin.Begin)
            {
                _pointer = pointer;
            }
            else if (seekOrigin == SeekOrigin.Current)
            {
                _pointer += pointer;
            }
            else
            {
                _pointer = _fileInfo.Size - pointer;
            }

            if (_pointer < 0)
            {
                _pointer = 0;
            }
        }

        public long ReadBytes(byte[] buffer, int offset, int length)
        {
            if (_pointer >= _fileInfo.Size)
            {
                return 0;
            }

            var bytesToRead = (long) length;
            var bytesRemainingInFile = _fileInfo.Size - _pointer;

            if (bytesToRead > bytesRemainingInFile)
            {
                bytesToRead = bytesRemainingInFile;
            }

            var sectorIndex = _pointer/Esfs.SectorSizeContentBytes;
            var bytesIndex = _pointer%Esfs.SectorSizeContentBytes;

            var bytesRemainingToRead = bytesToRead;
            var targetOffset = offset;

            while (bytesRemainingToRead > 0)
            {
                _chain.Seek((int) sectorIndex + 1);
                _chain.Read();

                var bytesAvailable = _chain.Data.Length - bytesIndex;
                if (bytesAvailable > bytesRemainingToRead)
                {
                    bytesAvailable = bytesRemainingToRead;
                }

                Array.Copy(_chain.Data, bytesIndex, buffer, targetOffset, bytesAvailable);

                bytesIndex = 0;
                sectorIndex++;

                bytesRemainingToRead -= bytesAvailable;
                targetOffset += (int) bytesAvailable;
            }

            _pointer += bytesToRead;

            return bytesToRead;
        }

        public void WriteBytes(byte[] buffer, int offset, int length)
        {
            var fileInfoNeed2BeUpdated = false;
            var needMoreBytes = (_pointer + length) - (_chain.Length() - 1) * Esfs.SectorSizeContentBytes;

            if (needMoreBytes > 0)
            {
                var needMoreSectors = (1 + needMoreBytes / Esfs.SectorSizeContentBytes) + AllocationChunkSectors;
                var appendix = _freespace.GetChunk((int) needMoreSectors);
                _chain.Glue(appendix);

                _fileInfo.EndSector = _chain.End();
                fileInfoNeed2BeUpdated = true;
            }

            var sectorIndex = _pointer / Esfs.SectorSizeContentBytes;
            var bytesIndex = _pointer % Esfs.SectorSizeContentBytes;

            var bytesToWrite = length;
            var sourceOffset = offset;

            while (bytesToWrite > 0)
            {
                _chain.Seek((int)sectorIndex + 1);

                var bytesAvailable = _chain.Data.Length - bytesIndex;

                if (bytesAvailable > bytesToWrite)
                {
                    bytesAvailable = bytesToWrite;
                }

                if (bytesIndex != 0 || bytesAvailable != _chain.Data.Length)
                {
                    _chain.Read();
                }

                Array.Copy(buffer, sourceOffset, _chain.Data, bytesIndex, bytesAvailable);
                _chain.Store();

                bytesIndex = 0;
                sectorIndex++;

                sourceOffset += (int) bytesAvailable;
                bytesToWrite -= (int) bytesAvailable;
            }

            _pointer += length;

            if (_pointer > _fileInfo.Size)
            {
                _fileInfo.Size = _pointer;
                fileInfoNeed2BeUpdated = true;
            }

            if (fileInfoNeed2BeUpdated)
            {
                StoreFileInfo();
            }
        }

        private void StoreFileInfo()
        {
            _chain.Seek(0);

            var fileInfo = _fileInfo.ToBytes();
            Array.Copy(fileInfo, _chain.Data, fileInfo.Length);

            _chain.Store();
        }
    }
}
