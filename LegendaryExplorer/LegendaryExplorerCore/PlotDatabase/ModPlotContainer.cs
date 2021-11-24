﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// Manages loading, saving, and accessing of multiple mod plot databases for a single game
    /// </summary>
    public class ModPlotContainer
    {
        public static int StartingModId = 100000;
        public MEGame Game { get; }
        public PlotElement GameHeader { get; }

        public List<ModPlotDatabase> Mods { get; } = new List<ModPlotDatabase>();

        public string LocalModFolderName => $"ModPlots{Game}";

        private int _highestModId = StartingModId + 1;

        public ModPlotContainer(MEGame game)
        {
            Game = game;
            GameHeader = new PlotElement(0, StartingModId, $"{game.ToLEVersion()}/{game.ToOTVersion()} Mods", PlotElementType.Region, 0,
                new List<PlotElement>());
        }

        public void AddMod(ModPlotDatabase mod)
        {
            mod.ModRoot.AssignParent(GameHeader);
            _highestModId = mod.ReindexElements(_highestModId);
            Mods.Add(mod);
        }

        public void RemoveMod(ModPlotDatabase mod, bool deleteFile = false, string appDataFolder = "")
        {
            mod.ModRoot.RemoveFromParent();
            Mods.Remove(mod);

            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            if (deleteFile && Directory.Exists(saveFolder))
            {
                var dbPath = Path.Combine(saveFolder, $"{mod.ModRoot.Label}.json");
                File.Delete(dbPath);
            }
        }

        public int GetNextElementId()
        {
            return _highestModId++;
        }

        /// <summary>
        /// Loads all mods for this game from the input AppData folder
        /// </summary>
        /// <param name="appDataFolder"></param>
        public void LoadModsFromDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
            var jsonFiles = new DirectoryInfo(saveFolder).EnumerateFiles().Where(f => f.Extension == ".json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    LoadModFromDisk(file);
                }
                catch
                {
                    Debug.WriteLine($"Unable to load Mod Plot Database at {file.FullName}");
                }
            }
        }

        /// <summary>
        /// Load an individual mod from disk
        /// </summary>
        /// <param name="modJsonPath">Path to mod .json file</param>
        public void LoadModFromDisk(string modJsonPath)
        {
            LoadModFromDisk(new FileInfo(modJsonPath));
        }

        /// <summary>
        /// Load an individual mod from disk
        /// </summary>
        /// <param name="file">FileInfo of mod .json file</param>
        /// <exception cref="Exception"></exception>
        public void LoadModFromDisk(FileInfo file)
        {
            if (!file.Exists || file.Extension != ".json") throw new Exception("Input path is not a JSON file");
            var newMod = new ModPlotDatabase() {Game = Game};
            newMod.LoadPlotsFromFile(file.FullName);
            foreach (var oldMod in Mods.Where(m => m.ModRoot.Label == newMod.ModRoot.Label))
            {
                RemoveMod(oldMod);
            }
            AddMod(newMod);
        }

        public void SaveModsToDisk(string appDataFolder)
        {
            var saveFolder = Path.Combine(appDataFolder, LocalModFolderName);
            foreach (var mod in Mods)
            {
                mod.SaveDatabaseToFile(saveFolder);
            }
        }
    }
}