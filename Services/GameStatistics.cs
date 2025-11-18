namespace EmpireGame
{
    public class GameStatistics
    {
        public int TurnsSurvived { get; set; }
        public int EnemyUnitsDestroyed { get; set; }
        public int UnitsLost { get; set; }
        public int MaxBasesOwned { get; set; }
        public int MaxCitiesOwned { get; set; }
        public int TotalBasesOwned { get; set; }
        public int TotalCitiesOwned { get; set; }
        public int StructuresCaptured { get; set; }
        public int StructuresLost { get; set; }
        public bool Victory { get; set; }
        public string PlayerName { get; set; }

        public GameStatistics()
        {
            TurnsSurvived = 0;
            EnemyUnitsDestroyed = 0;
            UnitsLost = 0;
            MaxBasesOwned = 0;
            MaxCitiesOwned = 0;
            TotalBasesOwned = 0;
            TotalCitiesOwned = 0;
            StructuresCaptured = 0;
            StructuresLost = 0;
            Victory = false;
            PlayerName = "Commander";
        }

        public int CalculateScore()
        {
            int score = 0;

            // Points for survival
            score += TurnsSurvived * 10;

            // Points for combat
            score += EnemyUnitsDestroyed * 50;
            score -= UnitsLost * 25;

            // Points for structures
            score += MaxBasesOwned * 200;
            score += MaxCitiesOwned * 300;
            score += StructuresCaptured * 100;
            score -= StructuresLost * 150;

            // Victory bonus
            if (Victory)
                score += 5000;

            return Math.Max(0, score);
        }

        public string GetRank()
        {
            int score = CalculateScore();

            if (score >= 10000) return "Supreme Commander";
            if (score >= 7500) return "Grand Marshal";
            if (score >= 5000) return "Field Marshal";
            if (score >= 3500) return "General";
            if (score >= 2500) return "Colonel";
            if (score >= 1500) return "Major";
            if (score >= 1000) return "Captain";
            if (score >= 500) return "Lieutenant";
            return "Recruit";
        }
    }
}