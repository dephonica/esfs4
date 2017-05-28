using System;
using System.Collections.Generic;
using System.Linq;

namespace EsfsLite
{
    public class EsfsChain : EsfsSector
    {
        private readonly object _lock = new object();

        private int _cacheIndex;
        private readonly List<long> _chainCache = new List<long>(8192);

        public EsfsChain(EsfsSal sal, Int64 baseSectorOffset) : base(sal)
        {
            Index = baseSectorOffset;
            Read();

            _cacheIndex = 0;
            _chainCache.Add(baseSectorOffset);
        }

        public void Next()
        {
            lock (_lock)
            {
                if ((_cacheIndex + 1) < _chainCache.Count)
                {
                    _cacheIndex++;
                    Index = _chainCache[_cacheIndex];
                }
                else
                {
                    Index = _chainCache[_cacheIndex];
                    Read();

                    if (Link == Index)
                    {
                        throw new EsfsException("Unable to follow next sector - end of chunk");
                    }

                    if (Sal.IsValidSector(Link) == false)
                    {
                        throw new EsfsException("Unable to follow next sector - invalid link");
                    }

                    if (_chainCache.Contains(Link))
                    {
                        throw new EsfsException("Unable to follow next sector - chain loop detected");
                    }

                    _chainCache.Add(Link);
                    _cacheIndex++;

                    Index = Link;
                }
            }
        }

        public bool IsCanGoForward()
        {
            if (Sal.IsValidSector(Link) == false ||
                Link == Index)
            {
                return false;
            }

            return true;
        }

        public void Seek(int sectorIndex)
        {
            lock (_lock)
            {
                while (sectorIndex >= _chainCache.Count)
                {
                    Next();
                }

                if (sectorIndex < _chainCache.Count)
                {
                    _cacheIndex = sectorIndex;
                    Index = _chainCache[sectorIndex];
                }
            }

            Read();
        }

        public long Start()
        {
            return _chainCache[0];
        }

        public long End()
        {
            Index = _chainCache.Last();
            Read();

            while (IsCanGoForward())
            {
                Next();
                Read();
            }

            return Index;
        }

        public int Length()
        {
            End();
            return _chainCache.Count;
        }

        public EsfsChain Split(int cutSectors)
        {
            var cutBegining = _chainCache[0];

            Seek(cutSectors);

            var cuttedChainBegining = Index;

            Seek(cutSectors - 1);

            Link = Index;

            Store();

            while (_chainCache[0] != cuttedChainBegining && _chainCache.Count > 0)
            {
                _chainCache.RemoveAt(0);

                if (_cacheIndex > 0)
                {
                    _cacheIndex--;
                }
            }

            return new EsfsChain(Sal, cutBegining);
        }

        public void Glue(EsfsChain chain)
        {
            End();
            Read();

            if (Link != Index)
            {
                throw new EsfsException(string.Format("Unable to glue chain - invalid link index in last sector ({0})", Link));
            }

            Link = chain.Start();
            Store();
        }
    }
}
