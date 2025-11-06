using System.Windows;
using System.Windows.Controls;

namespace EmpireGame
{
    public partial class StartGameForm : Window
    {
        public GameSettings Settings { get; private set; }

        public StartGameForm()
        {
            InitializeComponent();
        }

private void StartButton_Click(object sender, RoutedEventArgs e)
{
    // Validate commander name
    if (string.IsNullOrWhiteSpace(CommanderNameTextBox.Text))
    {
        MessageBox.Show("Please enter a commander name!", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    // Validate starting resources
    if (!int.TryParse(StartingGoldTextBox.Text, out int startingGold) || startingGold < 0)
    {
        MessageBox.Show("Please enter a valid starting gold amount (0 or greater)!", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    if (!int.TryParse(StartingSteelTextBox.Text, out int startingSteel) || startingSteel < 0)
    {
        MessageBox.Show("Please enter a valid starting steel amount (0 or greater)!", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    if (!int.TryParse(StartingOilTextBox.Text, out int startingOil) || startingOil < 0)
    {
        MessageBox.Show("Please enter a valid starting oil amount (0 or greater)!", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    // Create settings object
    Settings = new GameSettings
    {
        CommanderName = CommanderNameTextBox.Text.Trim(),
        MapType = (MapType)MapTypeCombo.SelectedIndex,
        MapSize = int.Parse(((ComboBoxItem)MapSizeCombo.SelectedItem).Tag.ToString()),
        NumberOfOpponents = OpponentsCombo.SelectedIndex + 1,
        Difficulty = (Difficulty)DifficultyCombo.SelectedIndex,
        ResourceAbundance = (ResourceAbundance)ResourcesCombo.SelectedIndex,
        StartingGold = startingGold,
        StartingSteel = startingSteel,
        StartingOil = startingOil,
        InitialTileSize = int.Parse(((ComboBoxItem)TileSizeCombo.SelectedItem).Tag.ToString()),
        AnimationDelay = int.Parse(((ComboBoxItem)AnimationSpeedCombo.SelectedItem).Tag.ToString())
    };

    DialogResult = true;
    Close();
}

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

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
        public int AnimationDelay { get; set; }    }

    public enum MapType
    {
        Continents,
        Pangea,
        Islands,
        Archipelago,
        PeninsulaAndIslands
    }

    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    public enum ResourceAbundance
    {
        Scarce,
        Normal,
        Abundant
    }
}