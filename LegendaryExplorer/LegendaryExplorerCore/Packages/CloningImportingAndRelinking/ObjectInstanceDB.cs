﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using Microsoft.Toolkit.HighPerformance;
using static System.Net.WebRequestMethods;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    /// <summary>
    /// Database class for mapping objects from an export in one game to the same one in another game
    /// </summary>
    public class ObjectInstanceDB
    {
        private readonly MEGame Game;

        private readonly List<string> FilePaths;

        private readonly CaseInsensitiveDictionary<ObjectInstanceInfo> ExportMap;

        private ObjectInstanceDB(MEGame game, List<string> filePaths, CaseInsensitiveDictionary<ObjectInstanceInfo> exportMap)
        {
            Game = game;
            FilePaths = filePaths;
            ExportMap = exportMap;
        }

        private const uint MAGIC = 0x1552D027;

        // V2 05/14/2024 - Add NetIndex
        private const uint CURRENT_VERSION = 2;

        public void Serialize(Stream outStream)
        {
            outStream.WriteUInt32(MAGIC);
            outStream.WriteUInt32(CURRENT_VERSION);

            outStream.WriteInt32(FilePaths.Count);
            foreach (string filePath in FilePaths)
            {
                outStream.WriteStringUtf8WithLength(filePath);
            }

            outStream.WriteInt32(ExportMap.Count);
            foreach ((string ifp, ObjectInstanceInfo oii) in ExportMap)
            {
                outStream.WriteStringUtf8WithLength(ifp);
                outStream.WriteInt32(oii.NetIndex); // V2
                outStream.WriteInt32(oii.Files.Count);
                outStream.Write(oii.Files.AsSpan().AsBytes());
            }
        }

        public static ObjectInstanceDB Deserialize(MEGame game, Stream inStream)
        {
            if (inStream.ReadUInt32() != MAGIC)
            {
                throw new Exception($"This is not a serialized {nameof(ObjectInstanceDB)}!");
            }
            switch (inStream.ReadUInt32())
            {
                case 1:
                    {
                        int filePathsCount = inStream.ReadInt32();
                        var filePaths = new List<string>(filePathsCount);
                        for (int i = 0; i < filePathsCount; i++)
                        {
                            filePaths.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int exportMapCount = inStream.ReadInt32();
                        var exportMap = new CaseInsensitiveDictionary<ObjectInstanceInfo>(exportMapCount);
                        for (int i = 0; i < exportMapCount; i++)
                        {
                            string key = inStream.ReadStringUtf8WithLength();
                            int filesCount = inStream.ReadInt32();
                            int[] files = new int[filesCount];
                            inStream.ReadToSpan(files.AsSpan().AsBytes());
                            exportMap.Add(key, new ObjectInstanceInfo(new List<int>(files), 0)); // V1 uses NetIndex 0 for everything.
                        }
                        return new ObjectInstanceDB(game, filePaths, exportMap);
                    }
                case 2:
                    {
                        int filePathsCount = inStream.ReadInt32();
                        var filePaths = new List<string>(filePathsCount);
                        for (int i = 0; i < filePathsCount; i++)
                        {
                            filePaths.Add(inStream.ReadStringUtf8WithLength());
                        }

                        int exportMapCount = inStream.ReadInt32();
                        var exportMap = new CaseInsensitiveDictionary<ObjectInstanceInfo>(exportMapCount);
                        for (int i = 0; i < exportMapCount; i++)
                        {
                            string key = inStream.ReadStringUtf8WithLength();
                            int netIndex = inStream.ReadInt32();

                            int filesCount = inStream.ReadInt32();
                            int[] files = new int[filesCount];
                            inStream.ReadToSpan(files.AsSpan().AsBytes());
                            exportMap.Add(key, new ObjectInstanceInfo(new List<int>(files), netIndex));
                        }
                        return new ObjectInstanceDB(game, filePaths, exportMap);
                    }
                case var unsupportedVersion:
                    throw new Exception($"{unsupportedVersion} is not a supported {nameof(ObjectInstanceDB)} version!");
            }
        }


        /// <summary>
        /// Returns a list of relative package file paths (to the game root) that contain the specified ExportEntry with the same InstancedFullPath. Returns null if the specified IFP is not in the database, indicating an object of that name does not exist in the game.
        /// </summary>
        /// <param name="ifp">Instanced full path of the object to find</param>
        /// <param name="localization">The localization for the InstancedFullPath object. This will force results to have a specific localization. If none is provided, all results are considered valid.</param>
        /// <returns></returns>
        public List<string> GetFilesContainingObject(string ifp, MELocalization localization = MELocalization.None)
        {
            if (ExportMap.TryGetValue(ifp, out ObjectInstanceInfo info))
            {
                return info.Files.Select(x => FilePaths[x]).Where(x => localization == MELocalization.None || x.GetUnrealLocalization() == localization).ToList();
            }

            return null;
        }

        public static ObjectInstanceDB Create(MEGame game, List<string> files, Action<int> numDoneReporter = null, Action<int> addExtraFiles = null)
        {
            var objectDB = new ObjectInstanceDB(game, new List<string>(files.Count), new CaseInsensitiveDictionary<ObjectInstanceInfo>());
            int numDone = 0;
            for (int i = 0; i < files.Count; i++)
            {
                numDone++;
                if (game == MEGame.ME3 && Path.GetFileName(files[i]) == "Default.sfar")
                {
                    // It's an SFAR package
                    DLCPackage dlc = new DLCPackage(files[i]);
                    addExtraFiles?.Invoke(dlc.Files.Count(x => x.isActualFile && x.FileName.RepresentsPackageFilePath()));
                    foreach (var f in dlc.Files)
                    {
                        if (f.isActualFile && f.FileName.RepresentsPackageFilePath())
                        {
                            var decompress = dlc.DecompressEntry(f);
                            using var dlcP = MEPackageHandler.UnsafePartialLoadFromStream(decompress, f.FileName, _ => false);
                            objectDB.AddFileToDB(dlcP, f.FileName);
                            numDone++;
                            numDoneReporter?.Invoke(numDone);
                        }
                    }
                }
                else if (files[i].RepresentsPackageFilePath())
                {
                    objectDB.AddFileToDB(files[i], false);
                }
                else
                {
                    // This should not happen.
                    Debugger.Break();
                }

                numDoneReporter?.Invoke(numDone);
            }
            return objectDB;
        }

        /// <summary>
        /// Adds objects to the db from the given package.
        /// </summary>
        /// <param name="package">Package object to inventory</param>
        /// <param name="filePath">Path to package file on disk</param>
        /// <param name="insertAtStart">If true, add new instances of objects to the start of a list, so they will be preferred.</param>
        public void AddFileToDB(IMEPackage package, string filePath, bool insertAtStart = true)
        {
            int filePathIndex = FilePaths.Count;
            if (package.Game != Game)
            {
                throw new InvalidOperationException($"Cannot add a {package.Game} file to a {Game} database");
            }

            string defaultGamePath = MEDirectories.GetDefaultGamePath(Game);
            if (package.FilePath.StartsWith(defaultGamePath))
            {
                // Store relative path
                FilePaths.Add(package.FilePath.Substring(defaultGamePath.Trim('\\', '/').Length + 1));
            }
            else
            {
                // Store full path
                FilePaths.Add(package.FilePath);
            }

            // Index objects
            foreach (ExportEntry exp in package.Exports)
            {
                string ifp = exp.InstancedFullPath;

                // Things to ignore
                if (ifp.StartsWith(@"TheWorld"))
                    continue;
                if (ifp.StartsWith(@"ObjectReferencer"))
                    continue;

                // Index it
                if (!ExportMap.TryGetValue(ifp, out ObjectInstanceInfo records))
                {
                    records = new ObjectInstanceInfo(new List<int>(), exp.NetIndex);
                    ExportMap.Add(ifp, records);
                }

                if (insertAtStart)
                {
                    records.Files.Insert(0, filePathIndex);
                }
                else
                {
                    records.Files.Add(filePathIndex);
                }
            }
        }

        /// <summary>
        /// Removes all entries associated with a specific package
        /// </summary>
        /// <param name="package"></param>
        public void RemoveFileFromDB(IMEPackage package)
        {
            var fileIndex = FilePaths.IndexOf(package.FilePath);
            foreach (ExportEntry exp in package.Exports)
            {
                if (ExportMap.TryGetValue(exp.InstancedFullPath, out ObjectInstanceInfo records))
                {
                    records.Files.RemoveAt(records.Files.IndexOf(fileIndex));
                    if (records.Files.Count == 0)
                    {
                        // Removed from DB entirely
                        ExportMap.Remove(exp.InstancedFullPath);
                    }
                }
            }

            FilePaths.RemoveAt(fileIndex);
        }

        /// <summary>
        /// Adds objects in file to db 
        /// </summary>
        /// <param name="filePath">Path to package file on disk</param>
        /// <param name="insertAtStart">If true, add new instances of objects to the start of a list, so they will be preferred.</param>
        //keep public; is used by external tools
        public void AddFileToDB(string filePath, bool insertAtStart = true)
        {
            // Must load data to access NetIndex.
            using IMEPackage package = MEPackageHandler.OpenMEPackage(filePath);
            AddFileToDB(package, filePath, insertAtStart);
        }
    }

    public class ObjectInstanceInfo
    {
        public List<int> Files;
        public int NetIndex;

        public ObjectInstanceInfo(List<int> files, int netIndex)
        {
            Files = files;
            NetIndex = netIndex;
        }
    }
}
