using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EsfsLite
{
    public class EsfsDirectory : IDisposable
    {
        private const int DirectoryItemSizeBytes = 24;

        private readonly MD5 _md5 = MD5.Create();

        private EsfsFileInfo _fileInfo = new EsfsFileInfo();

        public EsfsFreespace Freespace { get; set; }

        private readonly EsfsChain _dirChain;

        public EsfsDirectory(EsfsChain dirChain)
        {
            _dirChain = dirChain;
        }

        public void Dispose()
        {
            _md5.Dispose();
        }

        public IEnumerable<EsfsFileInfo> EnumerateDirectory(string mask)
        {
            var result = new List<EsfsFileInfo>();

            var directoryItemsCount = _dirChain.Data.Length / DirectoryItemSizeBytes;

            _dirChain.Seek(1);

            while (true)
            {
                _dirChain.Read();

                var recordOffset = 0;

                var sectorData = _dirChain.Data;

                for (var n = 0; n < directoryItemsCount; n++, recordOffset += DirectoryItemSizeBytes)
                {
                    var fileLink = BitConverter.ToInt64(sectorData,
                        recordOffset + DirectoryItemSizeBytes - sizeof (Int64));

                    if (fileLink > 0)
                    {
                        var fileInfo = ReadFileInfo(fileLink);

                        if (Regex.IsMatch(fileInfo.FileName, mask))
                        {
                            result.Add((EsfsFileInfo) fileInfo.Clone());
                        }
                    }
                }

                if (_dirChain.IsCanGoForward() == false)
                {
                    break;
                }

                _dirChain.Next();
            }

            return result;
        }

        public EsfsFileInfo CreateFile(string fileName, EsfsFileAttributes attribute, int fileSize)
        {
            if (Freespace == null)
            {
                throw new EsfsException("Freespace instance must be initialized for file creation");
            }

            var allocateSectors = 2 + fileSize/Esfs.SectorSizeContentBytes;
            var freeChunk = Freespace.GetChunk(allocateSectors);

            return CreateFileEx(fileName, attribute, fileSize, freeChunk.Start(), freeChunk.End());
        }

        public EsfsFileInfo CreateDirectory(string directoryName)
        {
            return CreateFile(directoryName, EsfsFileAttributes.Directory, 0);
        }

        public EsfsFileInfo CreateFileEx(string fileName, EsfsFileAttributes attribute, Int64 fileSize, Int64 fileInfoSector, Int64 endSector)
        {
            var timestamp = DateTime.Now;

            _fileInfo = new EsfsFileInfo
            {
                Attribute = attribute,
                Size = fileSize,
                TimestampCreated = timestamp,
                TimestampModifyed = timestamp,
                FileName = fileName,
                ParentDirectory = _dirChain.Start(),
                StartSector = fileInfoSector,
                EndSector = endSector
            };

            var fileNameHash = _md5.ComputeHash(Encoding.UTF8.GetBytes(fileName));

            var firstFreeOffset = FindFirstFreeItem();
            var directoryData = _dirChain.Data;

            Array.Copy(fileNameHash, 0, directoryData, firstFreeOffset, fileNameHash.Length);
            Array.Copy(BitConverter.GetBytes(fileInfoSector), 0, directoryData, firstFreeOffset + fileNameHash.Length,
                sizeof (Int64));

            _dirChain.Store();

            var sector = new EsfsSector(_dirChain.Sal) {Index = fileInfoSector};
            sector.Read();

            var sectorData = sector.Data;
            var fileInfoData = _fileInfo.ToBytes();

            Array.Copy(fileInfoData, sectorData, fileInfoData.Length);

            sector.Store();

            IncrementDirectoryFileCounter(1);

            return _fileInfo;
        }

        private void IncrementDirectoryFileCounter(int increment)
        {
            var sector = new EsfsSector(_dirChain.Sal) {Index = _dirChain.Start()};
            sector.Read();

            var fileInfo = new EsfsFileInfo();
            fileInfo.FromBytes(sector.Data, 0);

            fileInfo.EndSector = _dirChain.End();
            fileInfo.Size += increment;
            fileInfo.TimestampModifyed = DateTime.Now;

            var fileInfoBytes = fileInfo.ToBytes();
            Array.Copy(fileInfoBytes, sector.Data, fileInfoBytes.Length);

            sector.Store();
        }

        private int FindFirstFreeItem()
        {
            _dirChain.Seek(1);
            _dirChain.Read();

            var sectorData = _dirChain.Data;
            var directoryItemsCount = sectorData.Length/DirectoryItemSizeBytes;

            while (true)
            {
                var recordOffset = 0;

                for (var n = 0; n < directoryItemsCount; n++)
                {
                    var isEmpty = true;

                    for (var m = 0; m < DirectoryItemSizeBytes; m++)
                    {
                        if (sectorData[recordOffset + m] != 0)
                        {
                            isEmpty = false;
                            break;
                        }
                    }

                    if (isEmpty)
                    {
                        return recordOffset;
                    }

                    recordOffset += DirectoryItemSizeBytes;
                }

                if (_dirChain.IsCanGoForward() == false)
                {
                    ExpandDirectory();
                }

                _dirChain.Next();
                _dirChain.Read();
            }

        }

        private void ExpandDirectory()
        {
            if (Freespace == null)
            {
                throw new EsfsException("Unable to expand directory structure - Freespace handler not initialized yet");
            }

            var sectorLink = Freespace.GetChunk(1);

            _dirChain.Glue(sectorLink);
        }

        public EsfsFileInfo FindFile(string fileName)
        {
            var fileNameHash = _md5.ComputeHash(Encoding.UTF8.GetBytes(fileName));

            _dirChain.Seek(1);
            _dirChain.Read();

            while (true)
            {
                var foundLinks = FindFileInSectorCopy(fileNameHash);
                if (foundLinks.Count > 0)
                {
                    foreach (var link in foundLinks)
                    {
                        var fileInfo = ReadFileInfo(link);

                        if (fileInfo.FileName == fileName)
                        {
                            return (EsfsFileInfo) fileInfo.Clone();
                        }
                    }
                }

                if (_dirChain.IsCanGoForward())
                {
                    _dirChain.Next();
                    _dirChain.Read();
                }
                else
                {
                    return null;
                }
            }
        }

        public EsfsFileInfo GetDirectoryInfo()
        {
            return (EsfsFileInfo) ReadFileInfo(_dirChain.Start()).Clone();
        }

        private EsfsFileInfo ReadFileInfo(Int64 sectorIndex)
        {
            if (_dirChain.Sal.IsValidSector(sectorIndex) == false)
            {
                throw new EsfsException("Unable to read file info - invalid link to sector");
            }

            var sector = new EsfsSector(_dirChain.Sal) {Index = sectorIndex};
            sector.Read();

            _fileInfo.FromBytes(sector.Data, 0);

            return _fileInfo;
        }

        private List<Int64> FindFileInSectorCopy(byte[] md5Hash)
        {
            var dataBytes = _dirChain.Data;
            var singleRecordSizeBytes = md5Hash.Length + sizeof (Int64);
            var recordsPerSector = dataBytes.Length/singleRecordSizeBytes;

            var result = new List<Int64>();
            var compareArray = new byte[md5Hash.Length];

            var recordOffset = 0;

            for (var n = 0; n < recordsPerSector; n++)
            {
                Array.Copy(dataBytes, recordOffset, compareArray, 0, compareArray.Length);
                if (md5Hash.SequenceEqual(compareArray))
                {
                    result.Add(BitConverter.ToInt64(dataBytes, recordOffset + compareArray.Length));
                }

                recordOffset += singleRecordSizeBytes;
            }

            return result;
        }
    }
}

