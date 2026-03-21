namespace ArtOfTheTrade.Models
{
    public enum CaravanAnimalType { SumpterHorse, Mule, Camel }

    public class CaravanHand
    {
        public CaravanAnimalType AnimalType { get; set; }
        public int OutfitIndex { get; set; }
    }
}