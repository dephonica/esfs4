using System;

namespace EsfsLite
{
    public class EsfsSector
    {
        public EsfsSal Sal { get; }
        public Int64 Index { get; set; }

        private readonly byte[] _sectorData;
        private readonly byte[] _sectorContents;

        public EsfsSector(EsfsSal sal)
        {
            Sal = sal;
            _sectorData = new byte[Esfs.SectorSizeRawBytes];
            _sectorContents = new byte[Esfs.SectorSizeContentBytes];
        }

        public void Read()
        {
            Array.Copy(Sal.GetSector(Index), _sectorData, _sectorData.Length);
            Array.Copy(_sectorData, _sectorContents, _sectorContents.Length);
        }

        public void Store()
        {
            Array.Copy(_sectorContents, _sectorData, _sectorContents.Length);
            Sal.PutSector(Index, _sectorData, 0);
        }

        public byte[] Data
        {
            get
            {
                return _sectorContents;
            }

            set
            {
                Array.Copy(value, _sectorContents, _sectorContents.Length);
            }
        }

        public Int64 Link
        {
            get { return BitConverter.ToInt64(_sectorData, _sectorData.Length - sizeof (Int64)); }

            set
            {
                Array.Copy(BitConverter.GetBytes(value), 0, _sectorData, _sectorData.Length - sizeof (Int64),
                    sizeof (Int64));
            }
        }
    }
}
