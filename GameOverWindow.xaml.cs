using System.Windows;

namespace EmpireGame
{
    public partial class GameOverWindow : Window
    {
        public bool ReturnToMainMenu { get; private set; }

        public GameOverWindow(GameStatistics stats)
        {
            InitializeComponent();

            if (stats.Victory)
            {
                GameOverTitle.Text = "VICTORY!";
                GameOverTitle.Foreground = System.Windows.Media.Brushes.Gold;
                GameOverSubtitle.Text = "Your empire reigns supreme!";
            }

            RankText.Text = stats.GetRank();
            ScoreText.Text = stats.CalculateScore().ToString("N0");

            // Fill in statistics
            TurnsSurvivedText.Text = stats.TurnsSurvived.ToString();
            TurnsSurvivedPoints.Text = $"+{stats.TurnsSurvived * 10}";

            EnemyUnitsText.Text = stats.EnemyUnitsDestroyed.ToString();
            EnemyUnitsPoints.Text = $"+{stats.EnemyUnitsDestroyed * 50}";

            UnitsLostText.Text = stats.UnitsLost.ToString();
            UnitsLostPoints.Text = $"-{stats.UnitsLost * 25}";

            MaxBasesText.Text = stats.MaxBasesOwned.ToString();
            MaxBasesPoints.Text = $"+{stats.MaxBasesOwned * 200}";

            MaxCitiesText.Text = stats.MaxCitiesOwned.ToString();
            MaxCitiesPoints.Text = $"+{stats.MaxCitiesOwned * 300}";

            StructuresCapturedText.Text = stats.StructuresCaptured.ToString();
            StructuresCapturedPoints.Text = $"+{stats.StructuresCaptured * 100}";

            StructuresLostText.Text = stats.StructuresLost.ToString();
            StructuresLostPoints.Text = $"-{stats.StructuresLost * 150}";

            // Calculate bonuses
            int survivalBonus = stats.TurnsSurvived * 10;
            int combatBonus = (stats.EnemyUnitsDestroyed * 50) - (stats.UnitsLost * 25);
            int structureBonus = (stats.MaxBasesOwned * 200) + (stats.MaxCitiesOwned * 300) + 
                                 (stats.StructuresCaptured * 100) - (stats.StructuresLost * 150);

            SurvivalBonus.Text = $"+{survivalBonus}";
            CombatBonus.Text = combatBonus >= 0 ? $"+{combatBonus}" : combatBonus.ToString();
            StructureBonus.Text = structureBonus >= 0 ? $"+{structureBonus}" : structureBonus.ToString();
        }

        private void MainMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnToMainMenu = true;
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnToMainMenu = false;
            Close();
        }
    }
}