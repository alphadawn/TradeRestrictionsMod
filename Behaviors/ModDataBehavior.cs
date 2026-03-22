using TaleWorlds.CampaignSystem;
using ArtOfTheTrade.Save;

namespace ArtOfTheTrade.Behaviors
{
    /// <summary>
    /// Must be registered FIRST in SubModule so its SyncData runs before every
    /// other behavior's SyncData.  On load it reads the GUID from the game save
    /// and hydrates ModSaveManager.Data from the external JSON file.  On save it
    /// persists the GUID and flushes the JSON.  All other behaviors read/write
    /// their data directly via ModSaveManager.Data — their own SyncData methods
    /// are intentionally empty.
    /// </summary>
    public class ModDataBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading) ModSaveManager.Load(dataStore);
            if (dataStore.IsSaving)  ModSaveManager.Save(dataStore);
        }
    }
}
