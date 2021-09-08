﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for Kinkojiro experimental code
    /// </summary>
    public class PackageEditorExperimentsK
    {
        /// <summary>
        /// Copy ME2 Static art and collision into an ME3 file.
        /// By Kinkojiro
        /// </summary>
        /// <param name="Game">Target Game</param>
        /// <param name="BioPSource">Source BioP</param>
        /// <param name="tgtOutputfolder">OutputFolder</param>
        /// <param name="BioArtsToCopy">List of level source file locations</param>
        /// <param name="ActorsToMove">Dicitionary: key Actors, value filename, entry uid</param>
        /// <param name="AssetsToMove">Dictionary key: AssetInstancedPath, value filename, isimport, entry uid</param>
        /// <param name="fromreload">is reloaded json</param>
        public static async Task<List<string>> ConvertLevelToGame(MEGame Game, IMEPackage BioPSource, string tgtOutputfolder, string tgttfc, Action<string> callbackAction, LevelConversionData conversionData = null, bool fromreload = false, bool createtestlevel = false)
        {
            // TODO: IMPLEMENT IN LEX
            return null;

            /*
            //VARIABLES / VALIDATION
            var actorclassesToMove = new List<string>() { "BlockingVolume", "SpotLight", "SpotLightToggleable", "PointLight", "PointLightToggleable", "SkyLight", "HeightFog", "LenseFlareSource", "StaticMeshActor", "BioTriggerStream", "BioBlockingVolume" };
            var actorclassesToSubstitute = new Dictionary<string, string>()
            {
                { "BioBlockingVolume", "Engine.BlockingVolume" }
            };
            var archetypesToSubstitute = new Dictionary<string, string>()
            {
                { "Default__BioBlockingVolume", "Default__BlockingVolume" }
            };
            var fails = new List<string>();
            string busytext = null;
            if ((BioPSource.Game == MEGame.ME2 && ME2Directory.DefaultGamePath == null) || (BioPSource.Game == MEGame.ME1 && ME1Directory.DefaultGamePath == null) || (BioPSource.Game == MEGame.ME3 && ME3Directory.DefaultGamePath == null) || BioPSource.Game == MEGame.UDK)
            {
                fails.Add("Source Game Directory not found");
                return fails;
            }

            //Get filelist from BioP, Save a copy in outputdirectory, Collate Actors and asset information
            if (!fromreload)
            {
                busytext = "Collating level files...";
                callbackAction?.Invoke(busytext);
                conversionData = new LevelConversionData(Game, BioPSource.Game, null, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int, List<string>)>());
                var supportedExtensions = new List<string> { ".pcc", ".u", ".upk", ".sfm" };
                if (Path.GetFileName(BioPSource.FilePath).ToLowerInvariant().StartsWith("biop_") && BioPSource.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo") is ExportEntry BioWorld)
                {
                    string biopname = Path.GetFileNameWithoutExtension(BioPSource.FilePath);
                    conversionData.GameLevelName = biopname.Substring(5, biopname.Length - 5);
                    var lsks = BioWorld.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels").ToList();
                    if (lsks.IsEmpty())
                    {
                        fails.Add("No files found in level.");
                        return fails;
                    }
                    foreach (var l in lsks)
                    {
                        var lskexp = BioPSource.GetUExport(l.Value);
                        var filename = lskexp.GetProperty<NameProperty>("PackageName");
                        if ((filename?.Value.ToString().ToLowerInvariant().StartsWith("bioa") ?? false) || (filename?.Value.ToString().ToLowerInvariant().StartsWith("biod") ?? false))
                        {
                            var filePath = Directory.GetFiles(ME2Directory.DefaultGamePath, $"{filename.Value.Instanced}.pcc", SearchOption.AllDirectories).FirstOrDefault();
                            conversionData.FilesToCopy.TryAdd(filename.Value.Instanced, filePath);
                        }
                    }
                    conversionData.BioPSource = $"{biopname}_{BioPSource.Game}";
                    BioPSource.Save(Path.Combine(tgtOutputfolder, $"{conversionData.BioPSource}.pcc"));
                    BioPSource.Dispose();
                }
                else
                {
                    fails.Add("Requires an BioP to work with.");
                    return fails;
                }

                busytext = "Collating actors and assets...";
                callbackAction?.Invoke(busytext);
                Parallel.ForEach(conversionData.FilesToCopy, (pccref) =>
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(pccref.Value);
                    var sourcelevel = pcc.Exports.FirstOrDefault(l => l.ClassName == "Level");
                    if (ObjectBinary.From(sourcelevel) is Level levelbin)
                    {
                        foreach (var act in levelbin.Actors)
                        {
                            if (act < 1)
                                continue;
                            var actor = pcc.GetUExport(act);
                            if (actorclassesToMove.Contains(actor.ClassName))
                            {
                                conversionData.ActorsToMove.TryAdd($"{pccref.Key}.{actor.InstancedFullPath}", (pccref.Key, act));
                                HashSet<int> actorrefs = pcc.GetReferencedEntries(true, true, actor);
                                foreach (var r in actorrefs)
                                {
                                    var objref = pcc.GetEntry(r);
                                    if (objref != null)
                                    {
                                        if (objref.InstancedFullPath.Contains("PersistentLevel"))  //remove components of actors
                                            continue;
                                        string instancedPath = objref.InstancedFullPath;
                                        if (objref.idxLink == 0)
                                            instancedPath = $"{pccref.Key}.{instancedPath}";
                                        var added = conversionData.AssetsToMove.TryAdd(instancedPath, (pccref.Key, r, new List<string>() { pccref.Key }));
                                        if (!added)
                                        {
                                            conversionData.AssetsToMove[instancedPath].Item3.FindOrAdd(pccref.Key);
                                            if (r > 0 && conversionData.AssetsToMove[instancedPath].Item2 < 0) //Replace  imports with exports if possible
                                            {
                                                var currentlist = conversionData.AssetsToMove[instancedPath].Item3;
                                                conversionData.AssetsToMove[instancedPath] = (pccref.Key, r, currentlist);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Debug.WriteLine($"File Scanned: {pccref.Key}");
                    }
                });

                //Add assets from BioP into list if exports are in source
                foreach (var assetImport in conversionData.AssetsToMove.Where(t => t.Value.Item2 < 0))
                {
                    var biopExport = BioPSource.Exports.FirstOrDefault(x => x.InstancedFullPath == assetImport.Key);
                    if (biopExport != null)
                    {
                        var currentlist = conversionData.AssetsToMove[assetImport.Key].Item3;
                        conversionData.AssetsToMove[assetImport.Key] = (conversionData.BioPSource, biopExport.UIndex, currentlist);
                    }
                }
            }


            //Create ME2TempAssetsFile & ME3TempAssetsFile
            var SRCTempFileName = Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.SourceGame}.pcc");
            var TGTTempFileName = Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.TargetGame}.pcc");
            if (!fromreload)
            {
                busytext = "Copying assets to temporary file...";
                callbackAction?.Invoke(busytext);
                MEPackageHandler.CreateAndSavePackage(SRCTempFileName, conversionData.SourceGame);
                using (var srcassetfile = MEPackageHandler.OpenMEPackage(SRCTempFileName))
                {
                    SortedDictionary<string, string> sortedfiles = new SortedDictionary<string, string>(conversionData.FilesToCopy);
                    var assetCloneSourceQueue = new Queue<(string, string)>(); //Queue of files to load and copy in - biop => bioa => biod => then rest
                    assetCloneSourceQueue.Enqueue((conversionData.BioPSource, Path.Combine(tgtOutputfolder, $"{conversionData.BioPSource}.pcc")));
                    string masterbioa = $"BioA_{conversionData.GameLevelName}";
                    if (sortedfiles.ContainsKey(masterbioa))
                    {
                        assetCloneSourceQueue.Enqueue((masterbioa, conversionData.FilesToCopy[masterbioa]));
                        sortedfiles.Remove(masterbioa);
                    }
                    string masterbiod = $"BioD_{conversionData.GameLevelName}";
                    if (conversionData.FilesToCopy.ContainsKey(masterbiod))
                    {
                        assetCloneSourceQueue.Enqueue((masterbiod, conversionData.FilesToCopy[masterbiod]));
                        sortedfiles.Remove(masterbiod);
                    }
                    foreach (var bioa in sortedfiles)
                    {
                        assetCloneSourceQueue.Enqueue((bioa.Key, bioa.Value));
                    }

                    while (!assetCloneSourceQueue.IsEmpty())
                    {
                        var fileref = assetCloneSourceQueue.Dequeue();  //fileref item1 = pcc name, item2 = path
                        var levelpkg = MEPackageHandler.OpenMEPackage(fileref.Item2);
                        var assetsinpkg = conversionData.AssetsToMove.Where(a => a.Value.Item1.ToLowerInvariant() == fileref.Item1.ToLowerInvariant()).ToList(); //assets item1 = fullinstancedpath, item2 = filename, item3 = export Uindex
                        if (!levelpkg.IsNull())
                        {
                            foreach (var asset in assetsinpkg)
                            {
                                try
                                {
                                    var gotentry = levelpkg.TryGetEntry(asset.Value.Item2, out IEntry assetexp);
                                    if (asset.Key.StartsWith(fileref.Item1) && gotentry)  //fix to local package if item is in class (prevents conflicts)  - any shadow/lightmaps need special handling.
                                    {
                                        var localpackagexp = levelpkg.Exports.FirstOrDefault(x => x.ObjectName.Instanced == fileref.Item1);
                                        if (localpackagexp != null)
                                        {
                                            IEntry srclocalpkg = srcassetfile.Exports.FirstOrDefault(x => x.ObjectName.Instanced == fileref.Item1);
                                            if (srclocalpkg.IsNull())
                                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, localpackagexp, srcassetfile, null, false, out srclocalpkg);
                                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetexp, srcassetfile, srclocalpkg, true, out IEntry targetexp);
                                        }
                                        else
                                        {
                                            fails.Add($"Local Package not found on top level transfer: {fileref.Item2} : {asset.Key}");
                                        }
                                    }
                                    else if (gotentry)
                                    {
                                        var srcparent = EntryImporter.GetOrAddCrossImportOrPackage(assetexp.ParentFullPath, levelpkg, srcassetfile);
                                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetexp, srcassetfile, srcparent, true, out IEntry targetexp);
                                        Debug.WriteLine($"Asset Cloned Inbound: {fileref.Item1} : {asset.Key}");
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine($"Exception on asset {fileref.Item1} : {asset.Key}");
                                    fails.Add($"Exception on asset {fileref.Item1} : {asset.Key}");
                                }
                            }
                            levelpkg.Dispose();
                        }
                        else
                        {
                            Debug.WriteLine($"Asset Inbound Package load failure: {fileref.Item2}");
                            fails.Add($"Asset Inbound Package load failure: {fileref.Item2}");
                        }
                    }
                    srcassetfile.Save();

                    if (srcassetfile is MEPackage srcassetpack)
                    {
                        busytext = $"Converting to {conversionData.TargetGame}...";
                        callbackAction?.Invoke(busytext);
                        Debug.WriteLine($"Conversion started...");
                        if (tgttfc.IsNull())
                            tgttfc = $"Textures_{Path.GetFileName(tgtOutputfolder)}";
                        conversionData.TargetTFCName = tgttfc;
                        var tfcpath = Path.Combine(tgtOutputfolder, $"{tgttfc}.tfc");
                        srcassetpack.ConvertTo(conversionData.TargetGame, tfcpath, true);
                        srcassetpack.Save(TGTTempFileName);
                        srcassetpack.Dispose();
                        Debug.WriteLine($"Conversion finished.");
                    }
                    srcassetfile.Dispose();
                }

                //Export Settings to JSON
                busytext = "Exporting settings...";
                callbackAction?.Invoke(busytext);
                var srldata = JsonConvert.SerializeObject(conversionData);
                using (StreamWriter writer = File.CreateText(Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_Transfer.json")))
                {
                    writer.Write(srldata);
                }
                Debug.WriteLine($"Exported JSON");
            }

            //Create cooked files and populate with assets
            busytext = "Recooking assets out to individual files...";
            callbackAction?.Invoke(busytext);
            using (var assetfile = MEPackageHandler.OpenME3Package(TGTTempFileName))
            {
                foreach (var pccref in conversionData.FilesToCopy)
                {

                    var targetfile = Path.Combine(tgtOutputfolder, Path.GetFileName(pccref.Value));
                    if (createtestlevel)
                    {
                        if (pccref.Key.ToString().ToLowerInvariant().StartsWith("bioa"))
                            targetfile = Path.Combine(tgtOutputfolder, $"BioA_{conversionData.GameLevelName}_TEST.pcc");
                        else
                            targetfile = Path.Combine(tgtOutputfolder, $"BioD_{conversionData.GameLevelName}_TEST.pcc");
                        if (!File.Exists(targetfile))
                            File.Copy(Path.Combine(AppDirectories.ExecFolder, "ME3EmptyLevel.pcc"), targetfile);
                    }
                    else
                    {
                        if (File.Exists(targetfile))
                        {
                            File.Delete(targetfile);
                        }
                        File.Copy(Path.Combine(AppDirectories.ExecFolder, "ME3EmptyLevel.pcc"), targetfile);
                    }


                    using (var target = MEPackageHandler.OpenME3Package(targetfile))
                    using (var donor = MEPackageHandler.OpenME2Package(pccref.Value))
                    {
                        if (!createtestlevel)
                        {
                            for (int i = 0; i < target.Names.Count; i++)  //Setup new level file
                            {
                                string name = target.Names[i];
                                if (name.Equals("ME3EmptyLevel"))
                                {
                                    var newName = name.Replace("ME3EmptyLevel", Path.GetFileNameWithoutExtension(targetfile));
                                    target.replaceName(i, newName);
                                }
                            }
                            var packguid = Guid.NewGuid();
                            var package = target.GetUExport(1);
                            package.PackageGUID = packguid;
                            target.PackageGuid = packguid;
                            target.Save();
                        }
                        Debug.WriteLine($"Recooking outbound to {targetfile}");

                        var tgtlevel = target.GetUExport(target.Exports.FirstOrDefault(x => x.ClassName == "Level").UIndex);
                        //Get list of assets for this file & Process into
                        var sourceassets = conversionData.AssetsToMove.Where(s => s.Value.Item3.Contains(pccref.Key)).ToList();
                        foreach (var asset in sourceassets)
                        {
                            try
                            {
                                var assetexp = assetfile.Exports.FirstOrDefault(i => i.InstancedFullPath == asset.Key);
                                int assetUID = assetexp?.UIndex ?? 0;
                                if (assetUID == 0)
                                {
                                    var assetip = assetfile.Imports.FirstOrDefault(i => i.InstancedFullPath == asset.Key);
                                    assetUID = assetip?.UIndex ?? 0;
                                }
                                var gotasset = assetfile.TryGetEntry(assetUID, out IEntry assetent);
                                if (!gotasset)
                                {
                                    fails.Add($"Failure finding asset for outbound: {asset.Key}");
                                    continue;
                                }
                                IEntry tgtparent = null;
                                if (!asset.Key.StartsWith(pccref.Key))  //any shadow/lightmaps need special handling.
                                {
                                    tgtparent = EntryImporter.GetOrAddCrossImportOrPackage(assetent.ParentFullPath, assetfile, target);
                                }
                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetent, target, tgtparent, true, out IEntry targetexp);
                            }
                            catch
                            {
                                Debug.WriteLine($"Failure cloning outbound: {asset.Key}");
                                fails.Add($"Failure cloning outbound: {asset.Key}");
                            }
                        }
                        target.Save();
                        target.Dispose();
                        donor.Dispose();
                    }
                }
                assetfile.Dispose();
            }

            //Create cooked files and populate with actors
            busytext = "Recooking actors out to individual files...";
            callbackAction?.Invoke(busytext);
            Parallel.ForEach(conversionData.FilesToCopy, (pccref) =>
            {
                var targetfile = Path.Combine(tgtOutputfolder, Path.GetFileName(pccref.Value));
                using (var target = MEPackageHandler.OpenME3Package(targetfile))
                using (var donor = MEPackageHandler.OpenME2Package(pccref.Value))
                {
                    Debug.WriteLine($"Recooking actors out to {pccref.Value}");
                    var tgtlevel = target.GetUExport(target.Exports.FirstOrDefault(x => x.ClassName == "Level").UIndex);
                    var sourceactors = conversionData.ActorsToMove.Where(a => a.Value.Item1 == pccref.Key).ToList();
                    var newactors = new List<ExportEntry>();
                    foreach (var sactor in sourceactors)
                    {
                        var sactorxp = donor.GetUExport(sactor.Value.Item2);
                        if (actorclassesToSubstitute.ContainsKey(sactorxp.ClassName))
                        {
                            var oldclass = sactorxp.ClassName;
                            var newclass = actorclassesToSubstitute[oldclass];
                            sactorxp.Class = donor.getEntryOrAddImport(newclass);
                            var stack = sactorxp.GetPrePropBinary();
                            stack.OverwriteRange(0, BitConverter.GetBytes(sactorxp.Class.UIndex));
                            stack.OverwriteRange(4, BitConverter.GetBytes(sactorxp.Class.UIndex));
                            sactorxp.SetPrePropBinary(stack);
                            var children = sactorxp.GetChildren();
                            foreach (var c in children)
                            {
                                if (c is ExportEntry child && archetypesToSubstitute.ContainsKey(child.Archetype?.ParentName ?? "None"))
                                {
                                    var oldarchlink = child.Archetype.ParentName;
                                    var n = donor.FindNameOrAdd(oldarchlink);
                                    donor.replaceName(n, archetypesToSubstitute[oldarchlink]);
                                }
                            }
                        }
                        if (sactorxp?.HasArchetype ?? false)
                        {
                            sactorxp.CondenseArchetypes(true);
                        }
                        if (sactorxp != null)
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, sactorxp, target, tgtlevel, true, out IEntry addedactor);
                            newactors.Add((ExportEntry)addedactor);
                        }

                    }
                    target.AddToLevelActorsIfNotThere(newactors.ToArray());
                    target.Save();
                    target.Dispose();
                    donor.Dispose();
                }
            });
            Debug.WriteLine($"Done.");
            return fails;
            */
        }

        public async static Task<List<string>> RecookTransferLevelsFromJSON(string jsonfile, Action<string> callbackAction, bool CreateTestLevel = false)
        {
            var OutputDir = Path.GetDirectoryName(jsonfile);
            var conversionData = new LevelConversionData(MEGame.ME3, MEGame.ME2, null, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int, List<string>)>());
            IMEPackage sourcebiop = null;
            var fails = new List<string>();
            using (StreamReader file = File.OpenText(jsonfile))
            {
                JsonSerializer serializer = new JsonSerializer();
                conversionData = (LevelConversionData)serializer.Deserialize(file, typeof(LevelConversionData));
            }

            switch (conversionData.SourceGame)
            {
                case MEGame.ME2:
                    sourcebiop = MEPackageHandler.OpenME2Package(Path.Combine(OutputDir, $"{conversionData.BioPSource}.pcc"));
                    if (sourcebiop.IsNull())
                    {
                        fails.Add("Source BioP not found. Aborting.");
                        return fails;
                    }
                    break;
                default:
                    fails.Add("Currently level transfers only can be done ME2 to ME3");
                    return fails;
            }

            return await ConvertLevelToGame(conversionData.TargetGame, sourcebiop, OutputDir, conversionData.TargetTFCName, callbackAction, conversionData, true, CreateTestLevel);
        }

        public class LevelConversionData
        {
            public MEGame TargetGame = MEGame.ME3;
            public MEGame SourceGame = MEGame.ME2;
            public string GameLevelName = null;
            public string BioPSource = null;
            public string TargetTFCName = "Textures_DLC_MOD_";
            public ConcurrentDictionary<string, string> FilesToCopy = new ConcurrentDictionary<string, string>();
            public ConcurrentDictionary<string, (string, int)> ActorsToMove = new ConcurrentDictionary<string, (string, int)>();
            public ConcurrentDictionary<string, (string, int, List<string>)> AssetsToMove = new ConcurrentDictionary<string, (string, int, List<string>)>();

            public LevelConversionData(MEGame TargetGame, MEGame SourceGame, string GameLevelName, string BioPSource, string TargetTFCName, ConcurrentDictionary<string, string> FilesToCopy, ConcurrentDictionary<string, (string, int)> ActorsToMove, ConcurrentDictionary<string, (string, int, List<string>)> AssetsToMove)
            {
                this.TargetGame = TargetGame;
                this.SourceGame = SourceGame;
                this.GameLevelName = GameLevelName;
                this.BioPSource = BioPSource;
                this.TargetTFCName = TargetTFCName;
                this.FilesToCopy.AddRange(FilesToCopy);
                this.ActorsToMove.AddRange(ActorsToMove);
                this.AssetsToMove.AddRange(AssetsToMove);
            }

            public LevelConversionData()
            {

            }
        }

        public static void RestartTransferFromJSON(PackageEditorWindow pewpf, Action<EntryStringPair> entryDoubleClick)
        {
            CommonOpenFileDialog j = new CommonOpenFileDialog
            {
                DefaultExtension = ".json",
                EnsurePathExists = true,
                Title = "Select JSON with transfer details"
            };
            j.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
            if (j.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {

                pewpf.BusyText = "Recook level files";
                pewpf.IsBusy = true;
                Task.Run(() =>
                        PackageEditorExperimentsK.RecookTransferLevelsFromJSON(j.FileName,
                            newText => pewpf.BusyText = newText))
                    .ContinueWithOnUIThread(prevTask =>
                    {
                        if (pewpf.Pcc != null)
                            pewpf.LoadFile(pewpf.Pcc.FilePath);
                        pewpf.IsBusy = false;
                        var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask.Result.Count})", "",
                            pewpf)
                        {
                            DoubleClickEntryHandler = entryDoubleClick
                        };
                        dlg.Show();
                    });
            }
        }

        public static void RecookLevelToTestFromJSON(PackageEditorWindow pewpf, Action<EntryStringPair> entryDoubleClick)
        {
            CommonOpenFileDialog j = new CommonOpenFileDialog
            {
                DefaultExtension = ".json",
                EnsurePathExists = true,
                Title = "Select JSON with transfer details"
            };
            j.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
            if (j.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {

                pewpf.BusyText = "Recook level files";
                pewpf.IsBusy = true;
                Task.Run(() =>
                    PackageEditorExperimentsK.RecookTransferLevelsFromJSON(j.FileName, newText => pewpf.BusyText = newText,
                        true)).ContinueWithOnUIThread(prevTask =>
                {
                    if (pewpf.Pcc != null)
                        pewpf.LoadFile(pewpf.Pcc.FilePath);
                    pewpf.IsBusy = false;
                    var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask.Result.Count})", "", pewpf)
                    {
                        DoubleClickEntryHandler = entryDoubleClick
                    };
                    dlg.Show();
                });
            }
        }

        public static async void SaveAsNewPackage(PackageEditorWindow pewpf)
        {
            string fileFilter;
            switch (pewpf.Pcc.Game)
            {
                case MEGame.ME1:
                    fileFilter = GameFileFilters.ME1SaveFileFilter;
                    break;
                case MEGame.ME2:
                case MEGame.ME3:
                    fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                    break;
                default:
                    string extension = Path.GetExtension(pewpf.Pcc.FilePath);
                    fileFilter = $"*{extension}|*{extension}";
                    break;
            }

            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                string oldname = Path.GetFileNameWithoutExtension(pewpf.Pcc.FilePath).ToLower();
                string newname = Path.GetFileNameWithoutExtension(d.FileName);
                pewpf.Pcc.Save(d.FileName);
                pewpf.Pcc.Dispose();
                pewpf.Close();

                var p = new PackageEditorWindow();
                p.Show();
                p.LoadFile(d.FileName);
                p.Activate();
                for (int i = 0; i < p.Pcc.Names.Count; i++)
                {
                    string name = p.Pcc.Names[i];
                    if (name.ToLower() == oldname)
                    {
                        p.Pcc.replaceName(i, newname);
                        break;
                    }

                }

                Guid pkgguid = Guid.NewGuid();
                var localpackage = p.Pcc.Exports.FirstOrDefault<ExportEntry>(x => x.ClassName == "Package" && x.ObjectNameString == newname);
                if (localpackage != null)
                {
                    localpackage.PackageGUID = pkgguid;
                }
                p.Pcc.PackageGuid = pkgguid;

                await p.Pcc.SaveAsync();
                MessageBox.Show("New File Created and Loaded.");
            }
        }


        public static async void TrashCompactor(PackageEditorWindow pewpf, IMEPackage pcc)
        {
            var chkdlg = MessageBox.Show($"WARNING: Confirm you wish to recook this file?\n" +
                         $"\nThis will remove all references that current actors do not need.\nIt will then trash any entry that isn't being used.\n\n" +
                         $"This is an experimental tool. Make backups.", "Experimental Tool Warning", MessageBoxButton.OKCancel);
            if (chkdlg == MessageBoxResult.Cancel)
                return;
            pewpf.SetBusy("Finding unreferenced entries");
            ////pewpf.AllowRefresh = false;
            //Find all level references
            if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport)
            {
                Level level = ObjectBinary.From<Level>(levelExport);
                HashSet<int> norefsList = await Task.Run(() => pcc.GetReferencedEntries(false));
                pewpf.BusyText = "Recooking the Persistant Level";
                //Get all items in the persistent level not actors
                var references = new List<int>();
                foreach (var t in level.TextureToInstancesMap)
                {
                    references.Add(t.Key);
                }
                foreach (var txtref in references)
                {
                    if (norefsList.Contains(txtref) && txtref > 0)
                    {
                        level.TextureToInstancesMap.Remove(txtref);
                    }
                }
                references.Clear();

                //Clean up Cached PhysSM Data && Rebuild Data Store
                var newPhysSMmap = new OrderedMultiValueDictionary<UIndex, CachedPhysSMData>();
                var newPhysSMstore = new List<KCachedConvexData>();
                foreach (var r in level.CachedPhysSMDataMap)
                {
                    references.Add(r.Key);
                }
                foreach (int reference in references)
                {
                    if (!norefsList.Contains(reference) || reference < 0)
                    {
                        var map = level.CachedPhysSMDataMap[reference];
                        var oldidx = map.CachedDataIndex;
                        var kvp = level.CachedPhysSMDataStore[oldidx];
                        map.CachedDataIndex = newPhysSMstore.Count;
                        newPhysSMstore.Add(level.CachedPhysSMDataStore[oldidx]);
                        newPhysSMmap.Add(new KeyValuePair<UIndex, CachedPhysSMData>(reference, map));
                    }
                }
                level.CachedPhysSMDataMap = newPhysSMmap;
                level.CachedPhysSMDataStore = newPhysSMstore;
                references.Clear();

                //Clean up Cached PhysPerTri Data
                var newPhysPerTrimap = new OrderedMultiValueDictionary<UIndex, CachedPhysSMData>();
                var newPhysPerTristore = new List<KCachedPerTriData>();
                foreach (var s in level.CachedPhysPerTriSMDataMap)
                {
                    references.Add(s.Key);
                }
                foreach (int reference in references)
                {
                    if (!norefsList.Contains(reference) || reference < 0)
                    {
                        var map = level.CachedPhysPerTriSMDataMap[reference];
                        var oldidx = map.CachedDataIndex;
                        var kvp = level.CachedPhysPerTriSMDataStore[oldidx];
                        map.CachedDataIndex = newPhysPerTristore.Count;
                        newPhysPerTristore.Add(level.CachedPhysPerTriSMDataStore[oldidx]);
                        newPhysPerTrimap.Add(new KeyValuePair<UIndex, CachedPhysSMData>(reference, map));
                    }
                }
                level.CachedPhysPerTriSMDataMap = newPhysPerTrimap;
                level.CachedPhysPerTriSMDataStore = newPhysPerTristore;
                references.Clear();

                //Clean up NAV data - how to clean up Nav ints?
                if (norefsList.Contains(level.NavListStart ?? 0))
                {
                    level.NavListStart = 0;
                }
                if (norefsList.Contains(level.NavListEnd ?? 0))
                {
                    level.NavListEnd = 0;
                }
                var newNavArray = new List<UIndex>();
                newNavArray.AddRange(level.NavPoints);
                foreach (var navref in level.NavPoints)
                {
                    var navpoint = navref?.value ?? -1;
                    if (norefsList.Contains(navpoint) || navpoint == 0)
                    {
                        newNavArray.Remove(navref);
                    }
                }
                level.NavPoints = newNavArray;

                //Clean up Coverlink Lists => pare down guid2byte? table
                if (norefsList.Contains(level.CoverListStart ?? 0))
                {
                    level.CoverListStart = 0;
                }
                if (norefsList.Contains(level.CoverListEnd ?? 0))
                {
                    level.CoverListEnd = 0;
                }
                var newCLArray = new List<UIndex>();
                newCLArray.AddRange(level.CoverLinks);
                foreach (var clref in level.CoverLinks)
                {
                    var coverlink = clref?.value ?? -1;
                    if (norefsList.Contains(coverlink) || coverlink == 0)
                    {
                        newCLArray.Remove(clref);
                    }
                }
                level.CoverLinks = newCLArray;


                if (pcc.Game.IsGame3())
                {
                    //Clean up Pylon List
                    if (norefsList.Contains(level.PylonListStart ?? 0))
                    {
                        level.PylonListStart = 0;
                    }
                    if (norefsList.Contains(level.PylonListEnd ?? 0))
                    {
                        level.PylonListEnd = 0;
                    }
                }

                //Cross Level Actors
                level.CoverLinks = newCLArray;
                var newXLArray = new List<UIndex>();
                newXLArray.AddRange(level.CrossLevelActors);
                foreach (var cla in level.CrossLevelActors)
                {
                    var xlvlactor = cla?.value ?? -1;
                    if (norefsList.Contains(xlvlactor) || xlvlactor == 0)
                    {
                        newXLArray.Remove(cla);
                    }
                }
                level.CrossLevelActors = newXLArray;

                //Clean up int lists if empty of NAV points
                if (level.NavPoints.IsEmpty() && level.CoverLinks.IsEmpty() && level.CrossLevelActors.IsEmpty() && (!pcc.Game.IsGame3() || level.PylonListStart == 0))
                {
                    level.guidToIntMap.Clear();
                    level.guidToIntMap2.Clear();
                    level.intToByteMap.Clear();
                    level.numbers.Clear();
                }

                levelExport.WriteBinary(level);

                pewpf.BusyText = "Trashing unwanted items";
                List<IEntry> itemsToTrash = new List<IEntry>();
                foreach (var export in pcc.Exports)
                {
                    if (norefsList.Contains(export.UIndex))
                    {
                        itemsToTrash.Add(export);
                    }
                }
                //foreach (var import in pcc.Imports)  //Don't trash imports until UnrealScript functions can be fully parsed.
                //{
                //    if (norefsList.Contains(import.UIndex))
                //    {
                //        itemsToTrash.Add(import);
                //    }
                //}

                EntryPruner.TrashEntries(pcc, itemsToTrash);
            }
            //pewpf.AllowRefresh = true;
            pewpf.EndBusy();
            MessageBox.Show("Trash Compactor Done");
        }

        public static void NewSeekFreeFile(PackageEditorWindow pewpf)
        {
            string gameString = InputComboBoxDialog.GetValue(pewpf, "Choose game to create a seekfree file for:",
                                                          "Create new level file", new[] { "LE3", "LE2", "ME3", "ME2" }, "LE3");
            if (Enum.TryParse(gameString, out MEGame game) && game is MEGame.ME3 or MEGame.ME2 or MEGame.LE3 or MEGame.LE2)
            {
                var dlg = new SaveFileDialog
                {
                    Filter = GameFileFilters.ME3ME2SaveFileFilter,
                    OverwritePrompt = true
                };
                if (game.IsLEGame())
                {
                    dlg.Filter = GameFileFilters.LESaveFileFilter;
                }
                if (dlg.ShowDialog() == true)
                {
                    if (File.Exists(dlg.FileName))
                    {
                        File.Delete(dlg.FileName);
                    }
                    string emptyLevelName = game switch
                    {
                        MEGame.LE2 => "LE2EmptySeekFree",
                        MEGame.LE3 => "LE3EmptySeekFree",
                        MEGame.ME2 => "ME2EmptySeekFree",
                        _ => "ME3EmptySeekFree"
                    };
                    File.Copy(Path.Combine(AppDirectories.ExecFolder, $"{emptyLevelName}.pcc"), dlg.FileName);
                    pewpf.LoadFile(dlg.FileName);
                    for (int i = 0; i < pewpf.Pcc.Names.Count; i++)
                    {
                        string name = pewpf.Pcc.Names[i];
                        if (name.Equals(emptyLevelName))
                        {
                            var newName = name.Replace(emptyLevelName, Path.GetFileNameWithoutExtension(dlg.FileName));
                            pewpf.Pcc.replaceName(i, newName);
                        }
                    }

                    var packguid = Guid.NewGuid();
                    var package = pewpf.Pcc.GetUExport(game switch
                    {
                        MEGame.ME2 => 7,
                        _ => 1
                    });
                    package.PackageGUID = packguid;
                    pewpf.Pcc.PackageGuid = packguid;
                    pewpf.Pcc.Save();
                }
            }
        }

        public static void AddAllAssetsToReferencer(PackageEditorWindow pewpf)
        {
            if (pewpf.SelectedItem.Entry.ClassName != "ObjectReferencer")
            {
                MessageBox.Show("ObjectReferencer not selected.", "Error");
                return;
            }

            var oReferencer = pewpf.SelectedItem.Entry as ExportEntry;
            var referenceProp = oReferencer.GetProperties()?.GetProp<ArrayProperty<ObjectProperty>>("ReferencedObjects");

            var seekfreeClasses = new List<string>
            {
                "BioConversation",
                "FaceFXAnimSet",
                "Material",
                "MaterialInstanceConstant",
                "ObjectReferencer",
                "Sequence",
                "SkeletalMesh",
                "SkeletalMeshSocket",
                "Texture2D",
                "WwiseBank",
                "WwiseStream",
                "WwiseEvent"
            };
            var seekfreeAssets = new List<ObjectProperty>();
            foreach (var x in pewpf.Pcc.Exports)
            {
                foreach (var cls in seekfreeClasses)
                {
                    if (x.ClassName == cls)
                    {
                        var obj = new ObjectProperty(x);
                        seekfreeAssets.Add(obj);
                        break;
                    }
                }
            }

            if (referenceProp != null)
            {
                referenceProp.Clear();
                referenceProp.AddRange(seekfreeAssets);
                oReferencer.WriteProperty(referenceProp);
            }
        }

        public static void ChangeClassesGlobally(PackageEditorWindow pewpf)
        {

            if (pewpf.SelectedItem.Entry.ClassName != "Class")
            {
                MessageBox.Show("Class that is being replaced not selected.", "Error");
                return;
            }

            var replacement = EntrySelector.GetEntry<IEntry>(pewpf, pewpf.Pcc, "Select replacement Class reference");
            if (replacement == null || replacement.ClassName != "Class")
            {
                MessageBox.Show("Invalid replacement.", "Error");
                return;
            }

            int r = 0;
            foreach (var exp in pewpf.Pcc.Exports)
            {
                if (exp.Class == pewpf.SelectedItem.Entry && !exp.IsDefaultObject)
                {
                    exp.Class = replacement;
                    r++;
                    if(exp.ObjectName.Name == pewpf.SelectedItem.Entry.ObjectName.Name)
                    {
                        int idx = exp.indexValue;
                        exp.ObjectName = replacement.ObjectName.Name;
                        exp.indexValue = idx;
                    }
                }
            }

            MessageBox.Show($"{r} exports had classes replaced.", "Replace Classes");
        }
    }
}
