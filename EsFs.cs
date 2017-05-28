using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EsfsLite
{
    public class Esfs
    {
        public const int SectorSizeRawBytes = 1024;
        public const int SectorSizeContentBytes = SectorSizeRawBytes - sizeof(Int64);

        private const int FirstSectorOffset = 1;
        private const int RootDirectoryInfoSector = FirstSectorOffset;
        private const int FreeSpaceFirstSector = FirstSectorOffset + 3;

        private readonly EsfsSal _salMount;
        private readonly EsfsDirectory _rootDirectory;
        private readonly EsfsFreespace _freeSpace;

        public Esfs(Stream containerStream)
        {
            if (FirstSectorOffset < 1)
            {
                throw new EsfsException("Error - first sector offset for filesystem must be greater than zero");
            }

            _salMount = new EsfsSal(containerStream);

            if (containerStream.Length < SectorSizeRawBytes)
            {
                InitializeContainer();
            }

            var rootDirectoryChain = new EsfsChain(_salMount, RootDirectoryInfoSector);
            _rootDirectory = new EsfsDirectory(rootDirectoryChain);

            _freeSpace = new EsfsFreespace(_salMount, _rootDirectory);
            _rootDirectory.Freespace = _freeSpace;
        }

        private void InitializeContainer()
        {
            // 1. Make root directory sector
            var sector = new EsfsSector(_salMount) {Index = RootDirectoryInfoSector, Link = RootDirectoryInfoSector + 1};

            var rootInfo = new EsfsFileInfo
            {
                Attribute = EsfsFileAttributes.Directory,
                Size = 0,
                TimestampCreated = DateTime.Now,
                TimestampModifyed = DateTime.Now,
                FileName = "",
                ParentDirectory = 0,
                StartSector = RootDirectoryInfoSector,
                EndSector = RootDirectoryInfoSector + 1
            };

            var rootInfoBytes = rootInfo.ToBytes();
            Array.Copy(rootInfoBytes, sector.Data, rootInfoBytes.Length);

            sector.Store();

            sector.Index = sector.Link;
            Array.Copy(new byte[sector.Data.Length], sector.Data, sector.Data.Length);
            sector.Store();

            // 2. Make free space sectors
            var freeSpaceSectors = EsfsFreespace.MinimumFreeSpaceBytes/SectorSizeContentBytes;
            const long freeSpaceStart = (long) FreeSpaceFirstSector;

            for (var n = 0; n < freeSpaceSectors; n++)
            {
                sector.Index = freeSpaceStart + n;
                sector.Link = sector.Index + 1;
                sector.Store();
            }

            var freeSpaceEnd = sector.Index;
            sector.Link = freeSpaceEnd;
            sector.Store();

            // 3. Make free space file
            var chain = new EsfsChain(_salMount, RootDirectoryInfoSector);
            var directory = new EsfsDirectory(chain);

            var freeSpaceChain = new EsfsChain(_salMount, freeSpaceStart);

            directory.CreateFileEx("#freespace", EsfsFileAttributes.System, 
                (freeSpaceEnd - freeSpaceStart) * SectorSizeContentBytes, 
                freeSpaceChain.Start(),
                freeSpaceEnd);

            directory.Dispose();
        }

        public void CreateDirectory(string pathName)
        {
            CreateFile(pathName, EsfsFileAttributes.Directory);
        }

        public bool FileExists(string pathName)
        {
            return FindFile(pathName).ToArray().Any();
        }

        public void CreateFile(string pathName, EsfsFileAttributes attribute)
        {
            var directoryName = Path.GetDirectoryName(pathName);
            var fileName = Path.GetFileName(pathName);

            var directoryInstance = _rootDirectory;

            if (string.IsNullOrEmpty(directoryName) == false)
            {
                var directoryInfoEnum = FindFile(directoryName.Replace('\\', '/')).ToList();

                if (directoryInfoEnum.Any() == false)
                {
                    throw new EsfsException(string.Format("Unable to create file: No target directory '{0}' found",
                        directoryName));
                }

                var directoryInfo = directoryInfoEnum.First();

                var directoryChain = new EsfsChain(_salMount, directoryInfo.StartSector);
                directoryInstance = new EsfsDirectory(directoryChain) { Freespace = _freeSpace };
            }

            if (directoryInstance.FindFile(fileName) != null)
            {
                throw new EsfsException(
                    string.Format("Unable to create file: File '{0}' already exists in directory", pathName));
            }

            directoryInstance.CreateFile(fileName, attribute, 0);

            if (directoryInstance != _rootDirectory)
            {
                directoryInstance.Dispose();
            }
        }

        public IEnumerable<EsfsFileInfo> FindFile(string pathMask)
        {
            var pathMaskParts = pathMask.Split('/');

            var currentDirectory = _rootDirectory;

            if (pathMaskParts.Length > 0)
            {
                for (var n = 0; n < pathMaskParts.Length - 1; n++)
                {
                    var nextDirectoryName = pathMaskParts[n];

                    var fileInfo = currentDirectory.FindFile(nextDirectoryName);

                    if (fileInfo == null)
                    {
                        throw new EsfsException(string.Format("Invalid seatch path: no '{0}' directory found",
                            nextDirectoryName));
                    }

                    if (currentDirectory != _rootDirectory)
                    {
                        currentDirectory.Dispose();
                    }

                    currentDirectory = new EsfsDirectory(new EsfsChain(_salMount, fileInfo.StartSector))
                    {
                        Freespace = _freeSpace
                    };
                }

                var result = currentDirectory.EnumerateDirectory(pathMaskParts.Last());

                if (currentDirectory != _rootDirectory)
                {
                    currentDirectory.Dispose();
                }

                return result;
            }

            return null;
        }

        public EsfsFile OpenFile(string fileName)
        {
            var fileInfo = FindFile(fileName).ToArray();

            if (fileInfo.Any() == false)
            {
                throw new EsfsException(string.Format("Unable to open file '{0}' - no file found", fileName));
            }

            return new EsfsFile(_salMount, fileInfo.First(), _freeSpace);
        }
    }
}
