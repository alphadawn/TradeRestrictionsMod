using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ArtOfTheTrade.Save
{
    /// <summary>
    /// Manages one JSON file per campaign on disk.
    /// Each campaign is identified by a GUID stored as a single string in the game's
    /// native IDataStore — the only thing we ever put there.
    /// All real mod data lives in the JSON file.
    /// </summary>
    public static class ModSaveManager
    {
        private static string _campaignGuid;

        /// <summary>Live mod data for the current campaign session.</summary>
        public static ModSaveData Data { get; private set; } = new ModSaveData();

        // ── File paths ────────────────────────────────────────────────────────

        private static string SaveDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord",
                "ArtOfTradeSaves");

        private static string FilePath(string guid) =>
            Path.Combine(SaveDirectory, $"aot_{guid}.json");

        // ── Called by ModDataBehavior.SyncData ────────────────────────────────

        /// <summary>
        /// Load phase: read GUID from IDataStore, then load (or create) the JSON file.
        /// If no GUID exists this is a fresh campaign or the mod was just added.
        /// </summary>
        public static void Load(IDataStore dataStore)
        {
            var guid = string.Empty;
            dataStore.SyncData("ArtOfTheTrade_CampaignGuid", ref guid);

            // New campaign / mod freshly added — generate a brand-new identity
            if (string.IsNullOrEmpty(guid))
                guid = Guid.NewGuid().ToString("N");

            _campaignGuid = guid;

            var path = FilePath(guid);
            if (File.Exists(path))
            {
                try
                {
                    Data = JsonConvert.DeserializeObject<ModSaveData>(File.ReadAllText(path))
                           ?? new ModSaveData();
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"ArtOfTrade: Could not read save data ({ex.Message}) — starting fresh.",
                        Colors.Red));
                    Data = new ModSaveData();
                }
            }
            else
            {
                Data = new ModSaveData();
            }
        }

        /// <summary>
        /// Save phase: write GUID to IDataStore, then flush JSON to disk.
        /// </summary>
        public static void Save(IDataStore dataStore)
        {
            // On a brand-new game Load() may never have been called — generate GUID now so
            // the first save event still produces a valid JSON file.
            if (string.IsNullOrEmpty(_campaignGuid))
                _campaignGuid = Guid.NewGuid().ToString("N");

            dataStore.SyncData("ArtOfTheTrade_CampaignGuid", ref _campaignGuid);
            WriteToFile();
        }

        /// <summary>
        /// Write current Data to disk immediately.
        /// Can be called any time state changes for extra safety, though the save
        /// event is the authoritative flush point.
        /// </summary>
        public static void WriteToFile()
        {
            if (string.IsNullOrEmpty(_campaignGuid)) return;
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(
                    FilePath(_campaignGuid),
                    JsonConvert.SerializeObject(Data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"ArtOfTrade: Failed to write save file — {ex.Message}", Colors.Red));
            }
        }
    }
}
