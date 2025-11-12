using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EmpireGame
{
    public partial class CombatWindow : Window
    {
        private CombatResult combatResult;
        private int currentRoundIndex = 0;
        private bool skipAnimation = false;
        private bool allowEscape = true;
        private int animationSpeed = 2000; // Default 2 seconds per round
        private DispatcherTimer rollingTimer;
        private Random random = new Random();
        private bool combatComplete = false;

        public bool AttackerRetreated { get; private set; }

        // Player color mapping
        private static readonly Color[] PlayerColors = new Color[]
        {
            Color.FromRgb(0, 120, 255),      // Player 0 - Blue
            Color.FromRgb(255, 60, 60),      // Player 1 - Red
            Color.FromRgb(60, 255, 60),      // Player 2 - Green
            Color.FromRgb(255, 255, 60),     // Player 3 - Yellow
            Color.FromRgb(255, 140, 0),      // Player 4 - Orange
            Color.FromRgb(160, 60, 255),     // Player 5 - Purple
            Color.FromRgb(0, 255, 255),      // Player 6 - Cyan
            Color.FromRgb(255, 180, 200)     // Player 7 - Pink
        };

        public CombatWindow(CombatResult result)
        {
            InitializeComponent();
            combatResult = result;
            AttackerRetreated = false;

            // Load unit icons
            LoadUnitIcon(AttackerIcon, combatResult.Attacker);
            LoadUnitIcon(DefenderIcon, combatResult.Defender);

            // Set up initial display with player identification
            AttackerName.Text = $"{GetPlayerName(combatResult.Attacker.OwnerId)} {combatResult.Attacker.GetName()}";
            DefenderName.Text = $"{GetPlayerName(combatResult.Defender.OwnerId)} {combatResult.Defender.GetName()}";
            
            // Color code the names by player
            AttackerName.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Attacker.OwnerId));
            DefenderName.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Defender.OwnerId));
            
            UpdateLifeDisplay(false); // Initial display without animation

            // Start combat animation
            Loaded += async (s, e) =>
            {
                await Task.Delay(500); // Brief pause before starting
                await PlayCombat();
            };
        }

        private void LoadUnitIcon(Image imageControl, Unit unit)
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string iconBasePath = Path.Combine(appDirectory, "Resources\\Empire_Icons");
                
                // Determine if veteran
                string folder = unit.IsVeteran ? "Veteran" : "Units";
                
                // Map unit type to icon file name
                string unitTypeName = unit.GetType().Name;
                string iconFileName = unitTypeName;
                
                // Handle special cases
                if (unitTypeName == "Carrier")
                    iconFileName = "AircraftCarrier";
                
                string iconPath = Path.Combine(iconBasePath, folder, $"{iconFileName}.png");
                
                if (File.Exists(iconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imageControl.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load unit icon: {ex.Message}");
            }
        }

        private string GetPlayerName(int playerId)
        {
            string[] playerNames = { "Blue", "Red", "Green", "Yellow", "Orange", "Purple", "Cyan", "Pink" };
            if (playerId >= 0 && playerId < playerNames.Length)
                return playerNames[playerId];
            return $"Player {playerId + 1}";
        }

        private Color GetPlayerColor(int playerId)
        {
            if (playerId >= 0 && playerId < PlayerColors.Length)
                return PlayerColors[playerId];
            return Colors.White;
        }

        private void UpdateLifeDisplay(bool animate)
        {
            string attackerLifeText = $"Life: {combatResult.Attacker.Life}";
            string defenderLifeText = $"Life: {combatResult.Defender.Life}";

            if (animate)
            {
                // Animate attacker life change
                AnimateLifeChange(AttackerLife, AttackerLifeOld, attackerLifeText);
                
                // Animate defender life change
                AnimateLifeChange(DefenderLife, DefenderLifeOld, defenderLifeText);
            }
            else
            {
                AttackerLife.Text = attackerLifeText;
                DefenderLife.Text = defenderLifeText;
            }

            // Color code based on life
            AttackerLife.Foreground = combatResult.Attacker.Life > 0 ? Brushes.LimeGreen : Brushes.Red;
            DefenderLife.Foreground = combatResult.Defender.Life > 0 ? Brushes.LimeGreen : Brushes.Red;
        }

        private void AnimateLifeChange(TextBlock newLifeBlock, TextBlock oldLifeBlock, string newText)
        {
            // Only animate if the text is actually changing
            if (newLifeBlock.Text == newText)
                return;

            // Copy current text to old block
            oldLifeBlock.Text = newLifeBlock.Text;
            oldLifeBlock.Foreground = newLifeBlock.Foreground;
            oldLifeBlock.Opacity = 1;

            // Set new text
            newLifeBlock.Text = newText;
            newLifeBlock.Opacity = 0;

            // Animate old text dropping down and fading out
            var oldTransform = new TranslateTransform();
            oldLifeBlock.RenderTransform = oldTransform;

            var dropAnimation = new DoubleAnimation
            {
                From = 0,
                To = 30,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400)
            };

            // Animate new text fading in
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(100)
            };

            oldTransform.BeginAnimation(TranslateTransform.YProperty, dropAnimation);
            oldLifeBlock.BeginAnimation(OpacityProperty, fadeOutAnimation);
            newLifeBlock.BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        private async Task PlayCombat()
        {
            foreach (var round in combatResult.Rounds)
            {
                if (skipAnimation)
                {
                    // Apply all remaining rounds instantly
                    ApplyRoundResults(round);
                    continue;
                }

                // Show retreat button at the start of each round (except the first)
                if (currentRoundIndex > 0 && allowEscape && combatResult.Attacker.Life > 0)
                {
                    RetreatButton.Visibility = Visibility.Visible;
                }

                // Clear previous round result
                RoundResult.Text = "";

                // Animate rolling numbers
                await AnimateRolling();

                // Show final scores
                AttackerRoll.Text = round.AttackerScore.ToString();
                DefenderRoll.Text = round.DefenderScore.ToString();

                await Task.Delay(skipAnimation ? 100 : 800);

                // Show result
                if (round.Tie)
                {
                    RoundResult.Text = "TIE! Both units take damage!";
                    RoundResult.Foreground = Brushes.Orange;
                }
                else if (round.AttackerWon)
                {
                    RoundResult.Text = $"{GetPlayerName(combatResult.Attacker.OwnerId)} {combatResult.Attacker.GetName()} wins this round!";
                    RoundResult.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Attacker.OwnerId));
                }
                else
                {
                    RoundResult.Text = $"{GetPlayerName(combatResult.Defender.OwnerId)} {combatResult.Defender.GetName()} wins this round!";
                    RoundResult.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Defender.OwnerId));
                }

                await Task.Delay(skipAnimation ? 100 : 800);

                // Update life values with animation
                combatResult.Attacker.Life = round.AttackerLifeAfter;
                combatResult.Defender.Life = round.DefenderLifeAfter;
                UpdateLifeDisplay(true); // Animate the life change

                // PAUSE after life changes so user can see what happened
                await Task.Delay(skipAnimation ? 100 : 1200);

                RetreatButton.Visibility = Visibility.Collapsed;
                currentRoundIndex++;

                // Check if combat is over
                if (combatResult.Attacker.Life <= 0 || combatResult.Defender.Life <= 0)
                    break;
            }

            // Show final result
            await ShowFinalResult();
        }

        private void ApplyRoundResults(CombatRound round)
        {
            combatResult.Attacker.Life = round.AttackerLifeAfter;
            combatResult.Defender.Life = round.DefenderLifeAfter;
        }

        private async Task AnimateRolling()
        {
            int duration = skipAnimation ? 100 : animationSpeed;
            int steps = skipAnimation ? 5 : 20;
            int delayPerStep = duration / steps;

            for (int i = 0; i < steps; i++)
            {
                if (skipAnimation && i > 2)
                    break;

                AttackerRoll.Text = random.Next(0, 200).ToString();
                DefenderRoll.Text = random.Next(0, 200).ToString();

                await Task.Delay(delayPerStep);
            }
        }

        private async Task ShowFinalResult()
        {
            combatComplete = true;
            RoundResult.FontSize = 32;

            // Hide instructions and retreat button, show close button
            SpaceInstruction.Visibility = Visibility.Collapsed;
            RetreatButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;

            if (AttackerRetreated)
            {
                RoundResult.Text = $"{GetPlayerName(combatResult.Attacker.OwnerId)} {combatResult.Attacker.GetName()} retreated!";
                RoundResult.Foreground = Brushes.Yellow;
            }
            else if (combatResult.AttackerWon)
            {
                RoundResult.Text = $"{GetPlayerName(combatResult.Attacker.OwnerId)} {combatResult.Attacker.GetName()} WINS!";
                RoundResult.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Attacker.OwnerId));
                
                if (combatResult.Attacker.IsVeteran)
                {
                    await Task.Delay(500);
                    var veteranText = new TextBlock
                    {
                        Text = "★ VETERAN ★",
                        FontSize = 20,
                        Foreground = Brushes.Gold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    ((Grid)Content).Children.Add(veteranText);
                    Grid.SetRow(veteranText, 2);
                }
            }
            else
            {
                RoundResult.Text = $"{GetPlayerName(combatResult.Defender.OwnerId)} {combatResult.Defender.GetName()} WINS!";
                RoundResult.Foreground = new SolidColorBrush(GetPlayerColor(combatResult.Defender.OwnerId));
                
                if (combatResult.Defender.IsVeteran)
                {
                    await Task.Delay(500);
                    var veteranText = new TextBlock
                    {
                        Text = "★ VETERAN ★",
                        FontSize = 20,
                        Foreground = Brushes.Gold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    ((Grid)Content).Children.Add(veteranText);
                    Grid.SetRow(veteranText, 2);
                }
            }
        }

        private void RetreatButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentRoundIndex > 0 && combatResult.Attacker.Life > 0)
            {
                // Retreat
                AttackerRetreated = true;
                combatResult.AttackerEscaped = true;
                skipAnimation = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                skipAnimation = true;
            }
            else if (e.Key == Key.Escape && !combatComplete && currentRoundIndex > 0 && combatResult.Attacker.Life > 0)
            {
                // Retreat with ESC key
                AttackerRetreated = true;
                combatResult.AttackerEscaped = true;
                skipAnimation = true;
            }
            else if (e.Key == Key.Enter && combatComplete)
            {
                // Also allow Enter key to close after combat
                DialogResult = true;
                Close();
            }
        }
    }
}