using System.Windows;
using System.Windows.Controls;

namespace EmpireGame
{
    public partial class StartGameForm : Window
    {
        public GameSettings Settings { get; private set; }
        private List<ComboBox> aiPersonalityComboBoxes = new List<ComboBox>();

        public StartGameForm()
        {
            InitializeComponent();

            // Store references to AI personality combo boxes
            aiPersonalityComboBoxes.Add(AI1PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI2PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI3PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI4PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI5PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI6PersonalityCombo);
            aiPersonalityComboBoxes.Add(AI7PersonalityCombo);

            InitializeAIPersonalitySelectors();
            UpdateAIPersonalityVisibility();
        }

        private void InitializeAIPersonalitySelectors()
        {
            var personalities = AIPersonality.LoadPersonalities();

            for (int comboIndex = 0; comboIndex < aiPersonalityComboBoxes.Count; comboIndex++)
            {
                var combo = aiPersonalityComboBoxes[comboIndex];
                combo.Items.Clear();

                foreach (var personality in personalities)
                {
                    var item = new ComboBoxItem
                    {
                        Content = personality.ToString(),
                        Tag = personality
                    };
                    combo.Items.Add(item);
                }

                // Add event handler for selection changes
                combo.SelectionChanged += AIPersonalityCombo_SelectionChanged;

                // Set default selection - use different personality for each combo box
                if (comboIndex < personalities.Count)
                {
                    // Assign different personalities to each combo by index
                    var defaultPersonality = personalities[comboIndex];
                    foreach (ComboBoxItem item in combo.Items)
                    {
                        if (item.Tag is AIPersonality p && p.Name == defaultPersonality.Name)
                        {
                            combo.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    // If we have more combo boxes than personalities, start over
                    var defaultPersonality = personalities[comboIndex % personalities.Count];
                    foreach (ComboBoxItem item in combo.Items)
                    {
                        if (item.Tag is AIPersonality p && p.Name == defaultPersonality.Name)
                        {
                            combo.SelectedItem = item;
                            break;
                        }
                    }
                }
            }

            // Update availability to gray out duplicates if any are visible by default
            UpdateAIPersonalityAvailability();
        }
        private void AIPersonalityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAIPersonalityAvailability();
        }

        private void UpdateAIPersonalityAvailability()
        {
            int numberOfOpponents = OpponentsCombo.SelectedIndex + 1;

            // Collect all currently selected personalities
            var selectedPersonalities = new HashSet<string>();
            for (int i = 0; i < numberOfOpponents && i < aiPersonalityComboBoxes.Count; i++)
            {
                if (aiPersonalityComboBoxes[i].SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is AIPersonality selected)
                {
                    selectedPersonalities.Add(selected.Name);
                }
            }

            // Update each combo box
            for (int i = 0; i < numberOfOpponents && i < aiPersonalityComboBoxes.Count; i++)
            {
                var combo = aiPersonalityComboBoxes[i];

                // Get the currently selected personality (not the ComboBoxItem)
                AIPersonality currentSelection = null;
                if (combo.SelectedItem is ComboBoxItem currentItem && currentItem.Tag is AIPersonality currentPersonality)
                {
                    currentSelection = currentPersonality;
                }

                // Temporarily remove selection changed handler to avoid recursion
                combo.SelectionChanged -= AIPersonalityCombo_SelectionChanged;

                // Clear and rebuild items with proper enabled state
                combo.Items.Clear();

                var allPersonalities = AIPersonality.LoadPersonalities();
                ComboBoxItem itemToSelect = null;

                foreach (var personality in allPersonalities)
                {
                    var item = new ComboBoxItem
                    {
                        Content = personality.ToString(),
                        Tag = personality
                    };

                    // Disable if already selected in another combo box, but not if it's the current selection
                    if (selectedPersonalities.Contains(personality.Name) &&
                        (currentSelection == null || personality.Name != currentSelection.Name))
                    {
                        item.IsEnabled = false;
                        item.Foreground = System.Windows.Media.Brushes.Gray;
                    }

                    combo.Items.Add(item);

                    // Remember which item to select based on previous selection
                    if (currentSelection != null && personality.Name == currentSelection.Name)
                    {
                        itemToSelect = item;
                    }
                }

                // Restore previous selection
                if (itemToSelect != null)
                {
                    combo.SelectedItem = itemToSelect;
                }
                else
                {
                    // If nothing is selected, select the first available personality
                    foreach (ComboBoxItem item in combo.Items)
                    {
                        if (item.IsEnabled)
                        {
                            combo.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Re-add selection changed handler
                combo.SelectionChanged += AIPersonalityCombo_SelectionChanged;
            }
        }


        private void UpdateAIPersonalityVisibility()
        {
            int numberOfOpponents = OpponentsCombo.SelectedIndex + 1;

            for (int i = 0; i < aiPersonalityComboBoxes.Count; i++)
            {
                if (i < numberOfOpponents)
                {
                    aiPersonalityComboBoxes[i].Visibility = Visibility.Visible;
                }
                else
                {
                    aiPersonalityComboBoxes[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OpponentsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAIPersonalityVisibility();
        }

        private List<AIPersonality> GetSelectedAIPersonalities()
        {
            int numberOfOpponents = OpponentsCombo.SelectedIndex + 1;
            var selectedPersonalities = new List<AIPersonality>();
            var usedNames = new HashSet<string>();
            var allPersonalities = AIPersonality.LoadPersonalities();
            var random = new Random();

            for (int i = 0; i < numberOfOpponents; i++)
            {
                AIPersonality personality = null;

                if (i < aiPersonalityComboBoxes.Count &&
                    aiPersonalityComboBoxes[i].SelectedItem is AIPersonality selected)
                {
                    if (!usedNames.Contains(selected.Name))
                    {
                        personality = selected;
                    }
                    else
                    {
                        var availablePersonalities = allPersonalities
                            .Where(p => !usedNames.Contains(p.Name))
                            .ToList();

                        if (availablePersonalities.Count > 0)
                        {
                            personality = availablePersonalities[random.Next(availablePersonalities.Count)];
                        }
                    }
                }

                if (personality == null)
                {
                    var availablePersonalities = allPersonalities
                        .Where(p => !usedNames.Contains(p.Name))
                        .ToList();

                    if (availablePersonalities.Count > 0)
                    {
                        personality = availablePersonalities[random.Next(availablePersonalities.Count)];
                    }
                    else
                    {
                        personality = new AIPersonality($"AI Commander {i + 1}", AIPlaystyle.Balanced);
                    }
                }

                selectedPersonalities.Add(personality);
                usedNames.Add(personality.Name);
            }

            return selectedPersonalities;
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

            // Get selected AI personalities
            var aiPersonalities = GetSelectedAIPersonalities();

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
                AnimationDelay = int.Parse(((ComboBoxItem)AnimationSpeedCombo.SelectedItem).Tag.ToString()),
                AIPersonalities = aiPersonalities
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
}