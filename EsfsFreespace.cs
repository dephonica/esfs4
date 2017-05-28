using System;

namespace EsfsLite
{
    public class EsfsFreespace
    {
        public static int MinimumFreeSpaceBytes = 128*1024;

        private readonly EsfsFileInfo _freeSpaceInfo;
        private EsfsChain _freeSpaceChain;

        public EsfsFreespace(EsfsSal sal, EsfsDirectory rootDirectory)
        {
            _freeSpaceInfo = rootDirectory.FindFile("#freespace");
            if (_freeSpaceInfo == null)
            {
                throw new Exception("Error - unable to find #freespace system file");
            }

            _freeSpaceChain = new EsfsChain(sal, _freeSpaceInfo.StartSector);
        }

        public EsfsChain GetChunk(int sizeSectors)
        {
            CheckForExpandFreeSpace(sizeSectors);

            // 1. Cut freespace info sector
            var freeSpaceInfoChain = _freeSpaceChain.Split(1);

            // 2. Cut freespace chunk
            var chunkBegining = _freeSpaceChain.Split(sizeSectors);

            // 3. Glue freespace info sector with remaining freespace chunk
            freeSpaceInfoChain.Glue(_freeSpaceChain);

            _freeSpaceChain = freeSpaceInfoChain;

            UpdateFreeFile(_freeSpaceInfo.Size - sizeSectors * Esfs.SectorSizeContentBytes);

            return chunkBegining;
        }

        private void UpdateFreeFile(Int64 newFileSize)
        {
            _freeSpaceInfo.StartSector = _freeSpaceChain.Start();
            _freeSpaceInfo.Size = newFileSize;
            _freeSpaceInfo.TimestampModifyed = DateTime.Now;

            _freeSpaceChain.Index = _freeSpaceInfo.StartSector;
            _freeSpaceChain.Read();

            var sectorData = _freeSpaceChain.Data;
            var fileInfoBytes = _freeSpaceInfo.ToBytes();

            Array.Copy(fileInfoBytes, sectorData, fileInfoBytes.Length);
            _freeSpaceChain.Store();
        }

        private void CheckForExpandFreeSpace(int sizeSectors)
        {
            var requestedBytes = sizeSectors*Esfs.SectorSizeContentBytes;

            if (_freeSpaceInfo.Size - requestedBytes > MinimumFreeSpaceBytes)
            {
                return;
            }

            // Expand freespace file
            var lastFreeSector = _freeSpaceInfo.EndSector;

            var sector = new EsfsSector(_freeSpaceChain.Sal);

            var expandBySectors = sizeSectors + MinimumFreeSpaceBytes/Esfs.SectorSizeContentBytes;

            for (var n = 0; n < expandBySectors; n++)
            {
                sector.Index = lastFreeSector;
                sector.Read();

                lastFreeSector++;
                sector.Link = lastFreeSector;
                sector.Store();
            }

            sector.Link = sector.Index;
            sector.Store();

            _freeSpaceInfo.Size += (expandBySectors - 1)*Esfs.SectorSizeContentBytes;
            _freeSpaceInfo.EndSector = sector.Index;
            _freeSpaceInfo.TimestampModifyed = DateTime.Now;
        }
    }
}
