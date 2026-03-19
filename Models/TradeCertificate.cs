using TaleWorlds.CampaignSystem.Settlements;

namespace ArtOfTheTrade.Models
{
    public class TradeCertificate
    {
        public string TownId { get; set; }
        public float ExpiryDayTime { get; set; }

        public TradeCertificate() { }

        public TradeCertificate(Town town, float currentDayTime, float durationInDays)
        {
            TownId = town.StringId;
            ExpiryDayTime = currentDayTime + durationInDays;
        }

        public bool IsValid(float currentDayTime) => currentDayTime < ExpiryDayTime;
    }
}
