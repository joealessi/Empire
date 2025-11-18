using System.Collections.Generic;

namespace EmpireGame
{

    public class GameSettings
    {
        public string CommanderName { get; set; }
        public MapType MapType { get; set; }
        public int MapSize { get; set; }
        public int NumberOfOpponents { get; set; }
        public Difficulty Difficulty { get; set; }
        public ResourceAbundance ResourceAbundance { get; set; }
        public int StartingGold { get; set; }
        public int StartingSteel { get; set; }
        public int StartingOil { get; set; }
        public int InitialTileSize { get; set; }
        public int AnimationDelay { get; set; }
        public List<AIPersonality> AIPersonalities { get; set; }

        public GameSettings()
        {
            // Default values
            CommanderName = "Commander";
            MapType = MapType.Continents;
            MapSize = 100;
            NumberOfOpponents = 3;
            Difficulty = Difficulty.Normal;
            ResourceAbundance = ResourceAbundance.Normal;
            StartingGold = 10;
            StartingSteel = 5;
            StartingOil = 5;
            InitialTileSize = 32;
            AnimationDelay = 300;
            AIPersonalities = new List<AIPersonality>();
        }

        public int GetResourceMultiplier()
        {
            return ResourceAbundance switch
            {
                ResourceAbundance.Scarce => 1,
                ResourceAbundance.Normal => 2,
                ResourceAbundance.Abundant => 3,
                ResourceAbundance.Plentiful => 4,
                _ => 2
            };
        }

        public double GetAIBonusMultiplier()
        {
            return Difficulty switch
            {
                Difficulty.Easy => 0.75,
                Difficulty.Normal => 1.0,
                Difficulty.Hard => 1.25,
                Difficulty.Expert => 1.5,
                _ => 1.0
            };
        }
    }
}