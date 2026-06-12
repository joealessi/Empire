using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmpireGame
{
    public partial class MainWindow : Window
    {
        private Game game;
        private MapRenderer mapRenderer;
        private AIController aiController;
        private Unit selectedUnit;
        private Structure selectedStructure;
        private bool isSelectingBomberTarget;
        private Bomber bomberForMission;

        private bool isSelectingGeosyncLocation;
        private GeosynchronousSatellite geosyncToPlace;

        private int TILE_SIZE = 32;
        private const int MIN_TILE_SIZE = 16;
        private const int MAX_TILE_SIZE = 64;

        private int currentUnitIndex = 0;
        private int currentStructureIndex = 0;

        private const int MAX_UNITS_PER_TILE = 3;

        private Image endTurnImage;

        private MessageLog messageLog;

        private bool isSelectingPatrolWaypoints;
        private Unit unitOnPatrol;
        private List<TilePosition> patrolWaypoints;
        private TilePosition patrolStartPosition;

        private GameSettings gameSettings;

        private bool isSelectingBridgeTarget;
        private Sapper sapperForBridge;

        private bool _suppressExitConfirmation = false;

        public MainWindow()
        {
            InitializeComponent();

            // Show start game form (branding is merged into its header)
            StartGameForm startForm = new StartGameForm();
            if (startForm.ShowDialog() == true)
            {
                gameSettings = startForm.Settings;
                InitializeGame();
            }
            else
            {
                // User cancelled, close the game
                Close();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_suppressExitConfirmation && game != null)
            {
                var result = MessageDialog.Confirm(this, "Are you sure you want to exit?", "Confirm Exit");
                if (!result)
                    e.Cancel = true;
            }
        }

        private void InitializeGame()
        {
            messageLog = new MessageLog();

            // Use settings from the start form
            int mapSize = gameSettings.MapSize;
            int playerCount = gameSettings.NumberOfOpponents + 1; // +1 for human player
            TILE_SIZE = gameSettings.InitialTileSize;

            game = new Game(mapSize, mapSize, playerCount,
                gameSettings.StartingGold,
                gameSettings.StartingSteel,
                gameSettings.StartingOil);

            // Difficulty gives the AI an income advantage (Easy 0.75x .. Expert 1.5x; Normal = none)
            game.AIIncomeMultiplier = gameSettings.GetAIBonusMultiplier();

            // Set player name
            game.Players[0].Name = gameSettings.CommanderName;

            // Update AI players with their personalities (don't create new players!)
            for (int i = 1; i < playerCount; i++)
            {
                Player aiPlayer = game.Players[i]; // Use existing player

                // Assign personality from settings
                if (gameSettings.AIPersonalities != null && i - 1 < gameSettings.AIPersonalities.Count)
                {
                    aiPlayer.Personality = gameSettings.AIPersonalities[i - 1];
                    aiPlayer.Name = aiPlayer.Personality.Name;
                }
                else
                {
                    aiPlayer.Personality = new AIPersonality($"AI Commander {i}", AIPlaystyle.Balanced);
                    aiPlayer.Name = aiPlayer.Personality.Name;
                }
            }

            GenerateMap();

            mapRenderer = new MapRenderer(game, TILE_SIZE);

            aiController = new AIController(game);

            foreach (Player player in game.Players)
            {
                player.UpdateVision(game.Map);
            }

            // Verify all players have structures
            for (int i = 0; i < game.Players.Count; i++)
            {
                Player p = game.Players[i];
                System.Diagnostics.Debug.WriteLine($"Player {i} ({p.Name}): {p.Structures.Count} structures, {p.Units.Count} units");
                foreach (Structure structure in p.Structures)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {structure.GetName()} at ({structure.Position.X}, {structure.Position.Y})");
                }
            }

            EndTurnButton.ApplyTemplate();

            messageLog.AddMessage($"Welcome, {gameSettings.CommanderName}!", MessageType.Success);
            messageLog.AddMessage($"Empire Game initialized on {gameSettings.MapType} map.", MessageType.Success);
            messageLog.AddMessage($"Starting resources: 💰{gameSettings.StartingGold} ⚙️{gameSettings.StartingSteel} 🛢️{gameSettings.StartingOil}", MessageType.Info);

            // Debug messages...
            int totalOil = 0;
            int totalSteel = 0;
            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    Tile tile = game.Map.GetTile(new TilePosition(x, y));
                    if (tile.Resource == ResourceType.Oil) totalOil++;
                    if (tile.Resource == ResourceType.Steel) totalSteel++;
                }
            }
            messageLog.AddMessage($"Total resources placed: {totalOil} oil, {totalSteel} steel", MessageType.Info);

            UpdateMessageLog();

            RenderMap();
            UpdateGameInfo();
            UpdateNextButton();
            UpdateEndTurnButtonImage();
            UpdateResourceDisplay();
            UpdateZoomDisplay();
        }

        private void UpdateNextButton()
        {
            if (game.CurrentPlayer.IsAI)
            {
                NextUnitButton.Visibility = Visibility.Collapsed;
                return;
            }

            NextUnitButton.Visibility = Visibility.Visible;

            // Check for units with movement, not skipped, and not asleep
            List<Unit> unitsWithMovement = game.CurrentPlayer.Units
                .Where(u => u.Position.X >= 0 &&
                            u.MovementPoints >= 0.5 &&
                            !u.IsSkippedThisTurn &&
                            !u.IsAsleep)
                .ToList();

            if (unitsWithMovement.Count > 0)
            {
                NextUnitButton.Content = $"Next Unit ({unitsWithMovement.Count})";
            }
            else
            {
                NextUnitButton.Content = $"Next City/Base ({game.CurrentPlayer.Structures.Count})";
            }
        }

        private void NextUnitButton_Click(object sender, RoutedEventArgs e)
        {
            // Check for units with movement, not skipped, not asleep
            List<Unit> unitsWithMovement = game.CurrentPlayer.Units
                .Where(u => u.Position.X >= 0 &&
                            u.MovementPoints > 0 &&
                            !u.IsSkippedThisTurn &&
                            !u.IsAsleep)
                .ToList();

            if (unitsWithMovement.Count > 0)
            {
                // Cycle through units with movement
                if (currentUnitIndex >= unitsWithMovement.Count)
                {
                    currentUnitIndex = 0;
                }

                Unit unit = unitsWithMovement[currentUnitIndex];
                currentUnitIndex++;

                SelectUnit(unit);
                CenterOnPosition(unit.Position);
            }
            else if (game.CurrentPlayer.Structures.Count > 0)
            {
                // Cycle through structures
                if (currentStructureIndex >= game.CurrentPlayer.Structures.Count)
                {
                    currentStructureIndex = 0;
                }

                Structure structure = game.CurrentPlayer.Structures[currentStructureIndex];
                currentStructureIndex++;

                SelectStructure(structure);
                CenterOnPosition(structure.Position);
            }

            UpdateNextButton();
        }

        private void SkipTurnButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.SkipThisTurn();
                AddMessage($"{selectedUnit.GetName()} skipped for this turn", MessageType.Info);

                NextUnitButton_Click(sender, e);
            }
        }

        private void SleepButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.Sleep();
                AddMessage($"{selectedUnit.GetName()} is now asleep. Select it to wake it up.", MessageType.Info);

                NextUnitButton_Click(sender, e);
            }
        }

        private void WakeUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null && selectedUnit.IsAsleep)
            {
                selectedUnit.WakeUp();
                AddMessage($"{selectedUnit.GetName()} is now awake!", MessageType.Success);

                SelectUnit(selectedUnit);
                UpdateNextButton();
                RenderMap();
            }
        }


        private void CenterOnPosition(TilePosition pos)
        {
            RenderMap();

            double tilePixelX = pos.X * TILE_SIZE + TILE_SIZE / 2.0;
            double tilePixelY = pos.Y * TILE_SIZE + TILE_SIZE / 2.0;
            double offsetX = tilePixelX - MapScrollViewer.ViewportWidth / 2.0;
            double offsetY = tilePixelY - MapScrollViewer.ViewportHeight / 2.0;

            MapScrollViewer.ScrollToHorizontalOffset(Math.Max(0, offsetX));
            MapScrollViewer.ScrollToVerticalOffset(Math.Max(0, offsetY));
        }

        private void UnitIconBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (selectedUnit != null)
            {
                CenterOnPosition(selectedUnit.Position);
                FlashTile(selectedUnit.Position);
            }
        }

        private void FlashTile(TilePosition pos)
        {
            double cx = pos.X * TILE_SIZE + TILE_SIZE / 2.0;
            double cy = pos.Y * TILE_SIZE + TILE_SIZE / 2.0;
            double baseSize = TILE_SIZE * 1.8;

            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = baseSize,
                Height = baseSize,
                Stroke = System.Windows.Media.Brushes.Yellow,
                StrokeThickness = 3,
                Fill = System.Windows.Media.Brushes.Transparent,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(ring, cx - baseSize / 2);
            Canvas.SetTop(ring, cy - baseSize / 2);
            Canvas.SetZIndex(ring, 100);
            MapCanvas.Children.Add(ring);

            int ticks = 0;
            const int totalTicks = 20;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(45)
            };
            timer.Tick += (s, e) =>
            {
                ticks++;
                double progress = ticks / (double)totalTicks;
                double scale = 1.0 + progress * 1.8;
                double opacity = 1.0 - progress;

                ring.Width = baseSize * scale;
                ring.Height = baseSize * scale;
                Canvas.SetLeft(ring, cx - ring.Width / 2);
                Canvas.SetTop(ring, cy - ring.Height / 2);
                ring.Opacity = opacity;

                if (ticks >= totalTicks)
                {
                    timer.Stop();
                    if (MapCanvas.Children.Contains(ring))
                        MapCanvas.Children.Remove(ring);
                }
            };
            timer.Start();
        }

        // Remove or comment out SetupTestScenario - we don't need it anymore
        private void GenerateMap()
        {
            Random rand = new Random();

            // Step 1: Initialize everything as ocean
            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    TilePosition pos = new TilePosition(x, y);
                    Tile tile = game.Map.GetTile(pos);
                    tile.Terrain = TerrainType.Ocean;
                }
            }

            // Step 2: Generate land based on map type
            List<TilePosition> continentCenters = new List<TilePosition>();

            switch (gameSettings.MapType)
            {
                case MapType.Continents:
                    continentCenters = GenerateContinentsMap(rand);
                    break;
                case MapType.Pangea:
                    continentCenters = GeneratePangeaMap(rand);
                    break;
                case MapType.Islands:
                    continentCenters = GenerateIslandsMap(rand);
                    break;
                case MapType.Archipelago:
                    continentCenters = GenerateArchipelagoMap(rand);
                    break;
                case MapType.PeninsulaAndIslands:
                    continentCenters = GeneratePeninsulaMap(rand);
                    break;
            }

            // Step 3: Smooth the map to remove single-tile anomalies
            SmoothMap();

            // Step 4: Add terrain variety to land
            AddTerrainVariety(rand);

            // Step 5: Mark coastal water
            MarkCoastalWater();

            // Step 6: Place resource tiles based on abundance
            PlaceResourceTiles(rand);

            // Step 7: Place player starting positions
            PlacePlayerStartingPositions(continentCenters, rand);
        }

        private List<TilePosition> GenerateContinentsMap(Random rand)
        {
            // Generate several large continents (current implementation)
            int numberOfContinents = game.Players.Count + rand.Next(1, 3);
            List<TilePosition> continentCenters = new List<TilePosition>();

            for (int i = 0; i < numberOfContinents; i++)
            {
                int attempts = 0;
                TilePosition center;
                do
                {
                    center = new TilePosition(
                        rand.Next(15, game.Map.Width - 15),
                        rand.Next(15, game.Map.Height - 15));
                    attempts++;
                } while (attempts < 100 && IsTooCloseToOtherContinents(center, continentCenters, 30));

                continentCenters.Add(center);
                GenerateContinent(center, rand.Next(200, 400), rand);
            }

            return continentCenters;
        }

        private List<TilePosition> GeneratePangeaMap(Random rand)
        {
            // One massive supercontinent in the center
            List<TilePosition> continentCenters = new List<TilePosition>();

            TilePosition center = new TilePosition(game.Map.Width / 2, game.Map.Height / 2);
            continentCenters.Add(center);

            // Make it very large - 60% of map area
            int targetSize = (int)(game.Map.Width * game.Map.Height * 0.6);
            GenerateContinent(center, targetSize, rand);

            // Add a few small offshore islands
            for (int i = 0; i < 3; i++)
            {
                TilePosition islandCenter = new TilePosition(
                    rand.Next(10, game.Map.Width - 10),
                    rand.Next(10, game.Map.Height - 10));

                // Make sure it's not overlapping the main continent
                int distance = Math.Abs(islandCenter.X - center.X) + Math.Abs(islandCenter.Y - center.Y);
                if (distance > 40)
                {
                    continentCenters.Add(islandCenter);
                    GenerateContinent(islandCenter, rand.Next(30, 60), rand);
                }
            }

            return continentCenters;
        }

        private List<TilePosition> GenerateIslandsMap(Random rand)
        {
            // Many small islands scattered across the map
            List<TilePosition> continentCenters = new List<TilePosition>();

            int numberOfIslands = game.Players.Count * 3 + rand.Next(5, 10);

            for (int i = 0; i < numberOfIslands; i++)
            {
                int attempts = 0;
                TilePosition center;
                do
                {
                    center = new TilePosition(
                        rand.Next(10, game.Map.Width - 10),
                        rand.Next(10, game.Map.Height - 10));
                    attempts++;
                } while (attempts < 100 && IsTooCloseToOtherContinents(center, continentCenters, 15));

                continentCenters.Add(center);

                // Small islands
                int size = rand.Next(40, 100);
                GenerateContinent(center, size, rand);
            }

            return continentCenters;
        }

        private List<TilePosition> GenerateArchipelagoMap(Random rand)
        {
            // Chain of medium-sized islands
            List<TilePosition> continentCenters = new List<TilePosition>();

            // Create a curving chain across the map
            int numberOfIslands = game.Players.Count * 2 + rand.Next(3, 6);

            // Start position for the chain
            int startX = rand.Next(20, game.Map.Width / 3);
            int startY = rand.Next(20, game.Map.Height - 20);

            double angleIncrement = (Math.PI * 1.5) / numberOfIslands; // Curve across map
            double currentAngle = 0;

            for (int i = 0; i < numberOfIslands; i++)
            {
                // Calculate position along a curved path
                int x = startX + (int)((game.Map.Width - 40) * ((double)i / numberOfIslands));
                int y = startY + (int)(Math.Sin(currentAngle) * (game.Map.Height / 4));

                // Add some randomness
                x += rand.Next(-10, 10);
                y += rand.Next(-10, 10);

                // Clamp to valid range
                x = Math.Max(15, Math.Min(game.Map.Width - 15, x));
                y = Math.Max(15, Math.Min(game.Map.Height - 15, y));

                TilePosition center = new TilePosition(x, y);
                continentCenters.Add(center);

                // Medium-sized islands
                int size = rand.Next(100, 200);
                GenerateContinent(center, size, rand);

                currentAngle += angleIncrement;
            }

            return continentCenters;
        }

        private List<TilePosition> GeneratePeninsulaMap(Random rand)
        {
            // One large landmass with a peninsula, plus surrounding islands
            List<TilePosition> continentCenters = new List<TilePosition>();

            // Main continent
            TilePosition mainCenter = new TilePosition(
                game.Map.Width / 3,
                game.Map.Height / 2);
            continentCenters.Add(mainCenter);

            int mainSize = (int)(game.Map.Width * game.Map.Height * 0.35);
            GenerateContinent(mainCenter, mainSize, rand);

            // Peninsula extending from main continent
            TilePosition peninsulaStart = new TilePosition(
                mainCenter.X + rand.Next(20, 35),
                mainCenter.Y + rand.Next(-15, 15));

            // Generate peninsula as a stretched landmass
            GenerateContinent(peninsulaStart, rand.Next(150, 250), rand);

            // Add medium islands
            for (int i = 0; i < game.Players.Count; i++)
            {
                int attempts = 0;
                TilePosition center;
                do
                {
                    center = new TilePosition(
                        rand.Next(15, game.Map.Width - 15),
                        rand.Next(15, game.Map.Height - 15));
                    attempts++;
                } while (attempts < 100 && IsTooCloseToOtherContinents(center, continentCenters, 25));

                continentCenters.Add(center);
                GenerateContinent(center, rand.Next(120, 200), rand);
            }

            return continentCenters;
        }

        //private void PlaceResourceTiles(Random rand)
        //{
        //    // Determine resource counts based on abundance setting
        //    int oilMin, oilMax, steelMin, steelMax;

        //    switch (gameSettings.ResourceAbundance)
        //    {
        //        case ResourceAbundance.Scarce:
        //            oilMin = 6;
        //            oilMax = 10;
        //            steelMin = 6;
        //            steelMax = 10;
        //            break;
        //        case ResourceAbundance.Normal:
        //            oilMin = 12;
        //            oilMax = 18;
        //            steelMin = 12;
        //            steelMax = 18;
        //            break;
        //        case ResourceAbundance.Abundant:
        //            oilMin = 20;
        //            oilMax = 30;
        //            steelMin = 20;
        //            steelMax = 30;
        //            break;
        //        default:
        //            oilMin = 12;
        //            oilMax = 18;
        //            steelMin = 12;
        //            steelMax = 18;
        //            break;
        //    }

        //    // Scale with map size
        //    double sizeMultiplier = (game.Map.Width * game.Map.Height) / 10000.0; // 100x100 = 1.0
        //    oilMin = (int)(oilMin * sizeMultiplier);
        //    oilMax = (int)(oilMax * sizeMultiplier);
        //    steelMin = (int)(steelMin * sizeMultiplier);
        //    steelMax = (int)(steelMax * sizeMultiplier);

        //    int oilTiles = rand.Next(oilMin, oilMax + 1);
        //    PlaceResourceType(ResourceType.Oil, oilTiles, rand);

        //    int steelTiles = rand.Next(steelMin, steelMax + 1);
        //    PlaceResourceType(ResourceType.Steel, steelTiles, rand);
        //}
        private void PlaceResourceTiles(Random rand)
        {
            // Counts scale with map dimension (50x50 = baseline x1). Each mineable resource's
            // scarcity (how many) and allowed terrain (where) come from the registry.
            double sizeFactor = Math.Max(1.0, game.Map.Width / 50.0);

            foreach (var type in ResourceRegistry.Mineable)
            {
                var def = ResourceRegistry.Get(type);
                int count = (int)Math.Round(ResourceRegistry.BaseCount(def.Scarcity) * sizeFactor);
                PlaceResourceType(type, count, def.AllowedTerrain, rand);
            }
        }

        private void PlaceResourceType(ResourceType resourceType, int count,
            IReadOnlyList<TerrainType> allowedTerrain, Random rand)
        {
            if (allowedTerrain == null || allowedTerrain.Count == 0)
                return; // nothing to place on (e.g. a non-mineable resource)

            var allowed = new HashSet<TerrainType>(allowedTerrain);

            // Keep resources off the very edge, scaled to map size so small maps don't get
            // a huge barren border (the old hard-coded 10-tile border crammed 50x50 maps).
            int margin = Math.Max(2, Math.Min(game.Map.Width, game.Map.Height) / 25);

            // Positions already holding this resource type — we spread these evenly.
            var sameType = new List<TilePosition>();

            for (int i = 0; i < count; i++)
            {
                // Mitchell's best-candidate sampling: sample several valid spots and keep the
                // one farthest from the nearest existing tile of the same resource. Repeated,
                // this yields a near-uniform spread of each resource type across the whole map.
                TilePosition? best = null;
                double bestScore = double.NegativeInfinity;

                for (int s = 0; s < 40; s++)
                {
                    TilePosition? cand = RandomEmptyTile(allowed, margin, rand);
                    if (cand == null) break; // no allowed tile remaining

                    double score = NearestDistance(cand.Value, sameType);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cand;
                    }
                }

                if (best == null) break; // no valid terrain left
                game.Map.GetTile(best.Value).Resource = resourceType;
                sameType.Add(best.Value);
            }
        }

        // Picks a random empty tile of an allowed terrain (off the edge), preferring spots not
        // crowding any existing resource; relaxes the spacing requirement only as a last resort
        // so we never fail to find a tile while allowed terrain still has room.
        private TilePosition? RandomEmptyTile(HashSet<TerrainType> allowedTerrain, int margin, Random rand)
        {
            int w = game.Map.Width, h = game.Map.Height;

            for (int t = 0; t < 60; t++)
            {
                int x = rand.Next(margin, w - margin);
                int y = rand.Next(margin, h - margin);
                TilePosition pos = new TilePosition(x, y);
                Tile tile = game.Map.GetTile(pos);
                if (tile.Resource != ResourceType.None) continue;
                if (!allowedTerrain.Contains(tile.Terrain)) continue;
                if (!IsWellSpacedFromOtherResources(pos, 2)) continue; // no two resources touching
                return pos;
            }

            // Relaxed: any empty allowed tile off the edge (ignore the 2-tile gap).
            for (int t = 0; t < 250; t++)
            {
                int x = rand.Next(margin, w - margin);
                int y = rand.Next(margin, h - margin);
                TilePosition pos = new TilePosition(x, y);
                Tile tile = game.Map.GetTile(pos);
                if (tile.Resource == ResourceType.None && allowedTerrain.Contains(tile.Terrain))
                    return pos;
            }
            return null;
        }

        // Distance (in tiles) from pos to the nearest position in the list; large if empty.
        private double NearestDistance(TilePosition pos, List<TilePosition> others)
        {
            if (others.Count == 0) return double.MaxValue;
            double min = double.MaxValue;
            foreach (TilePosition o in others)
            {
                double dx = pos.X - o.X, dy = pos.Y - o.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < min) min = d;
            }
            return min;
        }

        private bool IsWellSpacedFromOtherResources(TilePosition pos, int minDistance)
        {
            for (int dx = -minDistance; dx <= minDistance; dx++)
            {
                for (int dy = -minDistance; dy <= minDistance; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    TilePosition checkPos = new TilePosition(pos.X + dx, pos.Y + dy);
                    if (game.Map.IsValidPosition(checkPos))
                    {
                        Tile tile = game.Map.GetTile(checkPos);
                        if (tile.Resource != ResourceType.None)
                            return false; // Too close to another resource
                    }
                }
            }
            return true;
        }

        private bool IsTooCloseToOtherContinents(TilePosition pos, List<TilePosition> centers, int minDistance)
        {
            foreach (TilePosition center in centers)
            {
                int distance = Math.Abs(pos.X - center.X) + Math.Abs(pos.Y - center.Y);
                if (distance < minDistance)
                    return true;
            }
            return false;
        }

        private void GenerateContinent(TilePosition center, int size, Random rand)
        {
            Queue<TilePosition> frontier = new Queue<TilePosition>();
            HashSet<TilePosition> visited = new HashSet<TilePosition>();

            frontier.Enqueue(center);
            visited.Add(center);

            int tilesPlaced = 0;

            // Add directional bias for more interesting shapes
            double[] directionBias = new double[8];
            for (int i = 0; i < 8; i++)
            {
                directionBias[i] = 0.5 + rand.NextDouble() * 0.5;
            }

            while (frontier.Count > 0 && tilesPlaced < size)
            {
                TilePosition current = frontier.Dequeue();
                Tile? tile = game.Map.GetTile(current);

                if (tile != null)
                {
                    tile.Terrain = TerrainType.Land;
                    tilesPlaced++;

                    int distanceFromCenter = Math.Abs(current.X - center.X) + Math.Abs(current.Y - center.Y);

                    // Base probability that varies more dramatically
                    double baseProbability = 1.0 - Math.Pow(distanceFromCenter / (size / 8.0), 1.5);
                    baseProbability = Math.Max(0.15, Math.Min(0.95, baseProbability));

                    // Add noise to probability for irregular coastlines
                    double noiseFactor = (rand.NextDouble() - 0.5) * 0.4;

                    // Use 8 directions instead of 4 for more organic shapes
                    int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
                    int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

                    for (int i = 0; i < 8; i++)
                    {
                        TilePosition neighborPos = new TilePosition(current.X + dx[i], current.Y + dy[i]);

                        if (game.Map.IsValidPosition(neighborPos) && !visited.Contains(neighborPos))
                        {
                            // Apply directional bias and noise for irregular shapes
                            double adjustedProbability = baseProbability * directionBias[i] + noiseFactor;

                            // Occasionally create tendrils reaching out
                            if (rand.NextDouble() < 0.05)
                            {
                                adjustedProbability += 0.3;
                            }

                            // Diagonal tiles are slightly less likely (creates more jagged coastlines)
                            if (i >= 4)
                            {
                                adjustedProbability *= 0.85;
                            }

                            adjustedProbability = Math.Max(0.05, Math.Min(0.98, adjustedProbability));

                            if (rand.NextDouble() < adjustedProbability)
                            {
                                visited.Add(neighborPos);
                                frontier.Enqueue(neighborPos);

                                // Randomly update direction bias to create changing coastline character
                                if (rand.NextDouble() < 0.1)
                                {
                                    directionBias[i] *= (0.7 + rand.NextDouble() * 0.6);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void SmoothMap()
        {
            // Remove single-tile islands and fill single-tile lakes
            for (int pass = 0; pass < 2; pass++)
            {
                bool[,] shouldFlip = new bool[game.Map.Width, game.Map.Height];

                for (int x = 1; x < game.Map.Width - 1; x++)
                {
                    for (int y = 1; y < game.Map.Height - 1; y++)
                    {
                        TilePosition pos = new TilePosition(x, y);
                        Tile tile = game.Map.GetTile(pos);

                        // Count land neighbors
                        int landNeighbors = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                TilePosition neighborPos = new TilePosition(x + dx, y + dy);
                                Tile? neighbor = game.Map.GetTile(neighborPos);

                                if (neighbor != null && neighbor.Terrain != TerrainType.Ocean &&
                                    neighbor.Terrain != TerrainType.CoastalWater)
                                {
                                    landNeighbors++;
                                }
                            }
                        }

                        // If this is land but surrounded mostly by water, make it water
                        if (tile.Terrain != TerrainType.Ocean && tile.Terrain != TerrainType.CoastalWater && landNeighbors < 3)
                        {
                            shouldFlip[x, y] = true;
                        }
                        // If this is water but surrounded mostly by land, make it land
                        else if ((tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater) && landNeighbors > 5)
                        {
                            shouldFlip[x, y] = true;
                        }
                    }
                }

                // Apply flips
                for (int x = 1; x < game.Map.Width - 1; x++)
                {
                    for (int y = 1; y < game.Map.Height - 1; y++)
                    {
                        if (shouldFlip[x, y])
                        {
                            TilePosition pos = new TilePosition(x, y);
                            Tile tile = game.Map.GetTile(pos);

                            if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater)
                                tile.Terrain = TerrainType.Land;
                            else
                                tile.Terrain = TerrainType.Ocean;
                        }
                    }
                }
            }
        }

        private void AddTerrainVariety(Random rand)
        {
            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    TilePosition pos = new TilePosition(x, y);
                    Tile tile = game.Map.GetTile(pos);

                    if (tile.Terrain == TerrainType.Land)
                    {
                        double roll = rand.NextDouble();

                        if (roll < 0.05)
                            tile.Terrain = TerrainType.Mountain;
                        else if (roll < 0.15)
                            tile.Terrain = TerrainType.Hills;
                        else if (roll < 0.35)
                            tile.Terrain = TerrainType.Forest;
                        else
                            tile.Terrain = TerrainType.Plains;
                    }
                }
            }
        }

        private void MarkCoastalWater()
        {
            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    TilePosition pos = new TilePosition(x, y);
                    Tile tile = game.Map.GetTile(pos);

                    if (tile.Terrain == TerrainType.Ocean)
                    {
                        // Check if adjacent to land
                        bool adjacentToLand = false;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                TilePosition neighborPos = new TilePosition(x + dx, y + dy);
                                if (game.Map.IsValidPosition(neighborPos))
                                {
                                    Tile neighbor = game.Map.GetTile(neighborPos);
                                    if (neighbor.Terrain != TerrainType.Ocean &&
                                        neighbor.Terrain != TerrainType.CoastalWater)
                                    {
                                        adjacentToLand = true;
                                        break;
                                    }
                                }
                            }
                            if (adjacentToLand) break;
                        }

                        if (adjacentToLand)
                            tile.Terrain = TerrainType.CoastalWater;
                    }
                }
            }
        }

        private void PlacePlayerStartingPositions(List<TilePosition> continentCenters, Random rand)
        {
            // Find the largest continents for player placement
            List<(TilePosition center, int size)> continentSizes = new List<(TilePosition, int)>();

            foreach (TilePosition center in continentCenters)
            {
                int size = MeasureContinentSize(center);
                continentSizes.Add((center, size));
            }

            // Sort by size, largest first
            continentSizes.Sort((a, b) => b.size.CompareTo(a.size));

            // Place each player on a different large continent if possible
            for (int i = 0; i < game.Players.Count; i++)
            {
                TilePosition startPos;

                if (i < continentSizes.Count)
                {
                    // Find a good spot near this continent center
                    startPos = FindSuitableStartingPosition(continentSizes[i].center, rand);
                }
                else
                {
                    // Fallback - find any land
                    startPos = FindAnyLandPosition(rand);
                }

                // Create starting City
                City cityStructure = (City)game.CreateStructure(typeof(City), startPos, i);

                // Check if near coast for naval production and shipyard
                bool hasWater = HasAdjacentWater(startPos);
                //cityStructure.CanProduceNaval = hasWater;
                //cityStructure.HasShipyard = hasWater;

                game.Players[i].Structures.Add(cityStructure);
                game.Map.GetTile(startPos).Structure = cityStructure;
                game.Map.GetTile(startPos).OwnerId = i;

                // Add starting units around the base
                PlaceStartingUnits(i, startPos, rand);
            }
        }

        private bool HasAdjacentWater(TilePosition pos)
        {
            int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
            int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

            for (int i = 0; i < 8; i++)
            {
                TilePosition checkPos = new TilePosition(pos.X + dx[i], pos.Y + dy[i]);
                if (game.Map.IsValidPosition(checkPos))
                {
                    Tile tile = game.Map.GetTile(checkPos);
                    if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int MeasureContinentSize(TilePosition start)
        {
            // Flood fill to measure continent size
            Queue<TilePosition> queue = new Queue<TilePosition>();
            HashSet<TilePosition> visited = new HashSet<TilePosition>();

            queue.Enqueue(start);
            visited.Add(start);
            int size = 0;

            while (queue.Count > 0)
            {
                TilePosition current = queue.Dequeue();
                Tile? tile = game.Map.GetTile(current);

                if (tile != null && tile.Terrain != TerrainType.Ocean &&
                    tile.Terrain != TerrainType.CoastalWater)
                {
                    size++;

                    // Add neighbors
                    int[] dx = { -1, 0, 1, 0 };
                    int[] dy = { 0, 1, 0, -1 };

                    for (int i = 0; i < 4; i++)
                    {
                        TilePosition neighborPos = new TilePosition(current.X + dx[i], current.Y + dy[i]);
                        if (game.Map.IsValidPosition(neighborPos) && !visited.Contains(neighborPos))
                        {
                            visited.Add(neighborPos);
                            queue.Enqueue(neighborPos);
                        }
                    }
                }
            }

            return size;
        }

        private TilePosition FindSuitableStartingPosition(TilePosition nearCenter, Random rand)
        {
            // Try to find plains or land near the continent center
            for (int radius = 0; radius < 30; radius++)
            {
                List<TilePosition> candidates = new List<TilePosition>();

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int x = nearCenter.X + dx;
                        int y = nearCenter.Y + dy;
                        TilePosition pos = new TilePosition(x, y);

                        if (game.Map.IsValidPosition(pos))
                        {
                            Tile tile = game.Map.GetTile(pos);
                            // Only accept plains or land (not forest, hills, mountains, or water)
                            if (tile.Terrain == TerrainType.Plains || tile.Terrain == TerrainType.Land)
                            {
                                candidates.Add(pos);
                            }
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    return candidates[rand.Next(candidates.Count)];
                }
            }
            // Fallback - find ANY land position
            return FindAnyLandPosition(rand);
        }

        private TilePosition FindAnyLandPosition(Random rand)
        {
            List<TilePosition> landPositions = new List<TilePosition>();

            for (int x = 10; x < game.Map.Width - 10; x += 5)
            {
                for (int y = 10; y < game.Map.Height - 10; y += 5)
                {
                    TilePosition pos = new TilePosition(x, y);
                    Tile tile = game.Map.GetTile(pos);

                    if (tile.Terrain == TerrainType.Plains ||
                        tile.Terrain == TerrainType.Land ||
                        tile.Terrain == TerrainType.Forest)
                    {
                        landPositions.Add(pos);
                    }
                }
            }

            if (landPositions.Count > 0)
            {
                return landPositions[rand.Next(landPositions.Count)];
            }

            // Last resort
            return new TilePosition(game.Map.Width / 2, game.Map.Height / 2);
        }

        private bool IsNearCoast(TilePosition pos)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dy = -3; dy <= 3; dy++)
                {
                    TilePosition checkPos = new TilePosition(pos.X + dx, pos.Y + dy);
                    if (game.Map.IsValidPosition(checkPos))
                    {
                        Tile tile = game.Map.GetTile(checkPos);
                        if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void PlaceStartingUnits(int playerId, TilePosition basePos, Random rand)
        {
            // Place 2 armies and 1 tank near the base
            List<TilePosition> positions = new List<TilePosition>();

            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dy = -3; dy <= 3; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip base position

                    TilePosition pos = new TilePosition(basePos.X + dx, basePos.Y + dy);
                    if (game.Map.IsValidPosition(pos))
                    {
                        Tile tile = game.Map.GetTile(pos);
                        // Only place units on walkable land
                        if ((tile.Terrain == TerrainType.Land ||
                             tile.Terrain == TerrainType.Plains ||
                             tile.Terrain == TerrainType.Forest ||
                             tile.Terrain == TerrainType.Hills) &&
                            tile.Structure == null &&
                            tile.Units.Count == 0)
                        {
                            positions.Add(pos);
                        }
                    }
                }
            }

            if (positions.Count >= 3)
            {
                // Shuffle positions
                for (int i = positions.Count - 1; i > 0; i--)
                {
                    int j = rand.Next(i + 1);
                    TilePosition temp = positions[i];
                    positions[i] = positions[j];
                    positions[j] = temp;
                }

                // Place 2 armies
                for (int i = 0; i < 2 && i < positions.Count; i++)
                {
                    Army army = new Army { Position = positions[i], OwnerId = playerId };
                    game.Players[playerId].Units.Add(army);
                    game.Map.GetTile(positions[i]).Units.Add(army);
                }

                // Place 1 tank
                if (positions.Count > 2)
                {
                    Tank tank = new Tank { Position = positions[2], OwnerId = playerId };
                    game.Players[playerId].Units.Add(tank);
                    game.Map.GetTile(positions[2]).Units.Add(tank);
                }
            }
        }

        private void SetupTestScenario()
        {
            // Create a base for player 0
            Base base1 = (Base)game.CreateStructure(typeof(Base), new TilePosition(10, 10), 0);
            base1.CanProduceNaval = false;
            game.Players[0].Structures.Add(base1);
            game.Map.GetTile(base1.Position).Structure = base1;

            // Create a base for player 1 (AI)
            Base base2 = (Base)game.CreateStructure(typeof(Base), new TilePosition(90, 90), 1);
            base2.CanProduceNaval = false;
            game.Players[1].Structures.Add(base2);
            game.Map.GetTile(base2.Position).Structure = base2;

            // Create some starting units for player 0
            Army army1 = new Army { UnitId = 1, Position = new TilePosition(11, 10), OwnerId = 0 };
            game.Players[0].Units.Add(army1);
            game.Map.GetTile(army1.Position).Units.Add(army1);

            Tank tank1 = new Tank { UnitId = 2, Position = new TilePosition(12, 10), OwnerId = 0 };
            game.Players[0].Units.Add(tank1);
            game.Map.GetTile(tank1.Position).Units.Add(tank1);

            // Update initial vision
            game.Players[0].UpdateVision(game.Map);
            game.Players[1].UpdateVision(game.Map);
        }

        private void RenderMap()
        {
            // ALWAYS render from human player's perspective (Player 0)
            Player humanPlayer = game.Players[0];

            WriteableBitmap bitmap = mapRenderer.RenderMap(humanPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            MapCanvas.Children.Clear();

            Image image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };

            Canvas.SetZIndex(image, 10);
            MapCanvas.Children.Add(image);

            RenderMineOverlays(humanPlayer);
        }

        // Supply lines are shown by default; toggled by ToggleSupplyLinesButton.
        private bool showSupplyLines = true;

        private void ToggleSupplyLinesButton_Click(object sender, RoutedEventArgs e)
        {
            showSupplyLines = !showSupplyLines;
            AddMessage(showSupplyLines ? "Supply lines shown." : "Supply lines hidden.", MessageType.Info);
            RenderMap();
        }

        // Draws mine markers and (when enabled) supply lines in the owner's player color
        // — solid when connected, dashed when the line is cut.
        private void RenderMineOverlays(Player humanPlayer)
        {
            double half = TILE_SIZE / 2.0;
            foreach (var p in game.Players)
            {
                foreach (var s in p.Structures)
                {
                    if (!(s is Mine mine)) continue;

                    bool ownedByHuman = mine.OwnerId == humanPlayer.PlayerId;
                    bool visible = humanPlayer.FogOfWar.TryGetValue(mine.Position, out var vis) &&
                                   vis == VisibilityLevel.Visible;
                    if (!ownedByHuman && !visible) continue;

                    // Supply line for the human's own mines, in the owner's player color
                    // (solid when connected, dashed when cut).
                    if (showSupplyLines && ownedByHuman && mine.SupplyPath != null && mine.SupplyPath.Count > 1)
                    {
                        var poly = new System.Windows.Shapes.Polyline
                        {
                            Stroke = GetPlayerBrush(mine.OwnerId),
                            StrokeThickness = 2,
                            Opacity = 0.9,
                            IsHitTestVisible = false
                        };
                        if (!mine.IsConnected)
                            poly.StrokeDashArray = new System.Windows.Media.DoubleCollection { 3, 3 };
                        foreach (var pt in mine.SupplyPath)
                            poly.Points.Add(new System.Windows.Point(pt.X * TILE_SIZE + half, pt.Y * TILE_SIZE + half));
                        Canvas.SetZIndex(poly, 11);
                        MapCanvas.Children.Add(poly);
                    }

                    // Generic mine glyph (placeholder icon).
                    var glyph = new TextBlock
                    {
                        Text = "⛏",
                        FontSize = Math.Max(10, TILE_SIZE / 2),
                        Foreground = System.Windows.Media.Brushes.White,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(glyph, mine.Position.X * TILE_SIZE + 2);
                    Canvas.SetTop(glyph, mine.Position.Y * TILE_SIZE);
                    Canvas.SetZIndex(glyph, 12);
                    MapCanvas.Children.Add(glyph);
                }
            }
        }

        private void RenderResourceIcons(Player renderPlayer)
        {
            int iconsAdded = 0;

            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    TilePosition pos = new TilePosition(x, y);
                    Tile tile = game.Map.GetTile(pos);

                    VisibilityLevel visibility = VisibilityLevel.Hidden;
                    if (renderPlayer.FogOfWar.ContainsKey(pos))
                    {
                        visibility = renderPlayer.FogOfWar[pos];
                    }

                    if (visibility == VisibilityLevel.Visible && tile.Resource != ResourceType.None)
                    {
                        try
                        {
                            Image resourceImage = new System.Windows.Controls.Image
                            {
                                Width = 20,
                                Height = 20
                            };

                            string imagePath = ResourceRegistry.IsMineable(tile.Resource)
                                ? ResourceRegistry.Get(tile.Resource).IconPath
                                : "";

                            try
                            {
                                resourceImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative));
                            }
                            catch
                            {
                                // Try absolute pack URI if relative fails
                                resourceImage.Source = new BitmapImage(new Uri($"pack://application:,,,{imagePath}"));
                            }

                            double canvasX = x * TILE_SIZE + TILE_SIZE - 22;
                            double canvasY = y * TILE_SIZE + 2;

                            Canvas.SetLeft(resourceImage, canvasX);
                            Canvas.SetTop(resourceImage, canvasY);
                            Canvas.SetZIndex(resourceImage, 1);

                            MapCanvas.Children.Add(resourceImage);
                            iconsAdded++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load resource icon at ({x},{y}), Resource: {tile.Resource}, Error: {ex.Message}");
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"Resource icons added to canvas: {iconsAdded}");
        }
        private void UpdateGameInfo()
        {
            TurnNumberText.Text = $"Turn: {game.TurnNumber}";

            if (game.CurrentPlayer.PlayerId == 0)
            {
                CurrentPlayerText.Text = $"Player: {game.CurrentPlayer.Name}";
            }
            else
            {
                CurrentPlayerText.Text = $"Player: {game.CurrentPlayer.Name}";
            }
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (game.HasSurrendered)
                return;

            Point clickPos = e.GetPosition(MapCanvas);
            TilePosition tilePos = new TilePosition((int)(clickPos.X / TILE_SIZE), (int)(clickPos.Y / TILE_SIZE));

            if (!game.Map.IsValidPosition(tilePos))
                return;

            if (isSelectingBomberTarget)
            {
                HandleBomberTargetSelection(tilePos);
                return;
            }

            if (isSelectingGeosyncLocation)
            {
                HandleGeosyncPlacement(tilePos);
                return;
            }

            if (isSelectingBridgeTarget && sapperForBridge != null)
            {
                if (sapperForBridge.CanBuildBridgeAt(tilePos, game.Map))
                {
                    sapperForBridge.StartBuildingBridge(tilePos);
                    AddMessage($"Sapper began building a bridge. 1 turn remaining.", MessageType.Info);
                    isSelectingBridgeTarget = false;
                    sapperForBridge = null;
                    SelectUnit(selectedUnit);
                    RenderMap();
                }
                else
                {
                    AddMessage("Cannot build bridge here! Must be single-tile water with land on 2+ sides.", MessageType.Error);
                }
                return;
            }

            // NEW: Handle patrol waypoint selection
            if (isSelectingPatrolWaypoints)
            {
                HandlePatrolWaypointSelection(tilePos);
                return;
            }

            Tile tile = game.Map.GetTile(tilePos);

            // Check for units at this position (exclude satellites and sappers - they're untouchable)
            List<Unit> friendlyUnits = tile.Units
                .Where(u => u.OwnerId == game.CurrentPlayer.PlayerId &&
                            !(u is Satellite) &&
                            !(u is Sapper sapper && (sapper.IsBuildingBase || sapper.IsBuildingBridge)))
                .ToList();

            if (friendlyUnits.Count > 1)
            {
                // Multiple units stacked - show selection dialog
                ShowUnitStackSelection(friendlyUnits, tilePos);
                return;
            }
            else if (friendlyUnits.Count == 1)
            {
                SelectUnit(friendlyUnits[0]);
                CenterOnPosition(friendlyUnits[0].Position);
                RenderMap();
                return;
            }

            // Check for structures at this position
            if (tile.Structure != null && tile.Structure.OwnerId == game.CurrentPlayer.PlayerId)
            {
                SelectStructure(tile.Structure);
                CenterOnPosition(tile.Structure.Position);
                RenderMap();
                return;
            }

            // Show tile information
            SelectTile(tilePos);
            RenderMap();
        }

        private void HandlePatrolWaypointSelection(TilePosition tilePos)
        {
            if (patrolWaypoints.Count >= 2)
            {
                AddMessage("Maximum 2 waypoints. Remove last waypoint or start patrol.", MessageType.Warning);
                return;
            }

            // Don't allow selecting the same position as start or previous waypoint
            if (tilePos.Equals(patrolStartPosition))
            {
                AddMessage("Cannot select starting position as waypoint!", MessageType.Warning);
                return;
            }

            if (patrolWaypoints.Count > 0 && tilePos.Equals(patrolWaypoints[patrolWaypoints.Count - 1]))
            {
                AddMessage("Cannot select same position twice!", MessageType.Warning);
                return;
            }

            patrolWaypoints.Add(tilePos);
            PatrolWaypointsList.Items.Add($"Waypoint {patrolWaypoints.Count}: ({tilePos.X}, {tilePos.Y})");

            AddMessage($"Waypoint {patrolWaypoints.Count} set at ({tilePos.X}, {tilePos.Y})", MessageType.Info);

            if (patrolWaypoints.Count == 2)
            {
                AddMessage("Maximum waypoints set. Click 'Start Patrol' to begin.", MessageType.Info);
            }
        }

        private void ClearLastWaypointButton_Click(object sender, RoutedEventArgs e)
        {
            if (patrolWaypoints.Count > 0)
            {
                patrolWaypoints.RemoveAt(patrolWaypoints.Count - 1);
                PatrolWaypointsList.Items.RemoveAt(PatrolWaypointsList.Items.Count - 1);
                AddMessage("Last waypoint removed", MessageType.Info);
            }
        }

        private void StartPatrolButton_Click(object sender, RoutedEventArgs e)
        {
            if (patrolWaypoints.Count == 0)
            {
                AddMessage("Must set at least one waypoint!", MessageType.Warning);
                return;
            }

            // Build the complete patrol route: Start → WP1 → WP2 → WP1 → Start
            List<TilePosition> fullRoute = new List<TilePosition>();
            fullRoute.Add(patrolStartPosition);  // Index 0 - Start

            // Add all waypoints (going out)
            foreach (TilePosition wp in patrolWaypoints)
            {
                fullRoute.Add(wp);  // WP1, WP2
            }

            // Return path - EXCLUDE the last waypoint (we're already there)
            // Go backwards through waypoints, stopping before the last one
            for (int i = patrolWaypoints.Count - 2; i >= 0; i--)
            {
                fullRoute.Add(patrolWaypoints[i]);  // WP1 only (not WP2 again)
            }

            // Complete the loop back to start
            fullRoute.Add(patrolStartPosition);

            // Debug message
            string routeDebug = "Patrol route: ";
            for (int i = 0; i < fullRoute.Count; i++)
            {
                routeDebug += $"({fullRoute[i].X},{fullRoute[i].Y})";
                if (i < fullRoute.Count - 1) routeDebug += " → ";
            }
            AddMessage(routeDebug, MessageType.Info);

            // Create patrol order
            AutomaticOrder patrolOrder = new AutomaticOrder(unitOnPatrol, patrolStartPosition, AutomaticOrderType.Patrol);
            patrolOrder.PatrolWaypoints = fullRoute;

            // Start at waypoint 1, not 0, since unit is already at position 0
            patrolOrder.CurrentWaypointIndex = 1;

            game.AutomaticOrdersQueue.Enqueue(patrolOrder);

            // Handle aircraft takeoff if needed
            if (unitOnPatrol is AirUnit airUnit && airUnit.HomeBaseId != -1)
            {
                Structure homeBase = null;
                foreach (Structure structure in game.CurrentPlayer.Structures)
                {
                    if (structure.StructureId == airUnit.HomeBaseId)
                    {
                        homeBase = structure;
                        break;
                    }
                }

                if (homeBase != null)
                {
                    TilePosition adjacentPos = FindAdjacentEmptyTile(homeBase.Position);
                    if (adjacentPos.X != -1)
                    {
                        if (homeBase is Base baseStructure)
                        {
                            baseStructure.Airport.Remove(airUnit);
                        }
                        else if (homeBase is City city)
                        {
                            city.Airport.Remove(airUnit);
                        }

                        airUnit.Position = adjacentPos;
                        airUnit.HomeBaseId = -1;
                        airUnit.Fuel = airUnit.MaxFuel;

                        Tile tile = game.Map.GetTile(adjacentPos);
                        tile.Units.Add(airUnit);

                        AddMessage($"{airUnit.GetName()} took off and beginning patrol", MessageType.Movement);
                    }
                }
            }
            else
            {
                AddMessage($"{unitOnPatrol.GetName()} starting patrol with {patrolWaypoints.Count} waypoint(s)", MessageType.Movement);
            }

            isSelectingPatrolWaypoints = false;
            unitOnPatrol = null;
            patrolWaypoints = null;
            PatrolSetupPanel.Visibility = Visibility.Collapsed;

            _ = ProcessAutomaticOrdersWithVisuals();
        }

        private void CancelPatrolButton_Click(object sender, RoutedEventArgs e)
        {
            isSelectingPatrolWaypoints = false;
            unitOnPatrol = null;
            patrolWaypoints = null;
            PatrolSetupPanel.Visibility = Visibility.Collapsed;

            AddMessage("Patrol setup cancelled", MessageType.Info);
        }

        private void MapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (game.HasSurrendered)
                return;

            if (selectedUnit == null)
                return;

            Point clickPos = e.GetPosition(MapCanvas);
            TilePosition tilePos = new TilePosition((int)(clickPos.X / TILE_SIZE), (int)(clickPos.Y / TILE_SIZE));

            if (!game.Map.IsValidPosition(tilePos))
                return;

            // Issue move order
            MoveUnit(selectedUnit, tilePos);
        }

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Could show hover information here
        }

        private void SelectUnit(Unit unit)
        {
            selectedUnit = unit;
            selectedStructure = null;

            if (unit == null)
            {
                UnitInfoPanel.Visibility = Visibility.Collapsed;
                StructureInfoPanel.Visibility = Visibility.Collapsed;
                TileInfoPanel.Visibility = Visibility.Collapsed;
                return;
            }

            UnitInfoPanel.Visibility = Visibility.Visible;
            StructureInfoPanel.Visibility = Visibility.Collapsed;
            TileInfoPanel.Visibility = Visibility.Collapsed;

            UnitNameText.Text = $"{unit.GetName()} ({(unit.IsVeteran ? "Veteran" : "Regular")})";
            UnitStatsText.Text = $"Power: {unit.Power} | Toughness: {unit.Toughness}";

            // Set unit icon and player color border
            UpdateUnitIcon(unit);

            // Update Life Bar
            double lifePercent = ((double)unit.Life / unit.MaxLife) * 100;
            LifeProgressBar.Value = lifePercent;
            LifeProgressText.Text = $"{unit.Life}/{unit.MaxLife}";

            if (lifePercent > 66)
                LifeProgressBar.Foreground = System.Windows.Media.Brushes.LimeGreen;
            else if (lifePercent > 33)
                LifeProgressBar.Foreground = System.Windows.Media.Brushes.Yellow;
            else
                LifeProgressBar.Foreground = System.Windows.Media.Brushes.Red;

            // Update Movement Bar
            double movementPercent = (unit.MovementPoints / unit.MaxMovementPoints) * 100;
            MovementProgressBar.Value = movementPercent;
            MovementProgressText.Text = $"{unit.MovementPoints:F1}/{unit.MaxMovementPoints}";

            if (movementPercent > 50)
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.DodgerBlue;
            else if (movementPercent > 0)
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
            else
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.DarkGray;

            // Hide all specialized buttons and panels by default
            HideAllSpecializedButtons();

            FuelGaugePanel.Visibility = Visibility.Collapsed;
            SubmarinePanel.Visibility = Visibility.Collapsed;
            CarrierCapacityPanel.Visibility = Visibility.Collapsed;
            PatrolBoatWarningText.Visibility = Visibility.Collapsed;
            ArtilleryRangeText.Visibility = Visibility.Collapsed;
            AntiAircraftProximityText.Visibility = Visibility.Collapsed;
            SpyStatusText.Visibility = Visibility.Collapsed;
            SapperBuildText.Visibility = Visibility.Collapsed;

            // Show/hide Sleep vs Wake Up based on unit state
            if (unit.IsAsleep)
            {
                CircularSleepButton.Visibility = Visibility.Collapsed;
                CircularWakeUpButton.Visibility = Visibility.Visible;
            }
            else
            {
                CircularSleepButton.Visibility = Visibility.Visible;
                CircularWakeUpButton.Visibility = Visibility.Collapsed;
            }

            // Handle unit-specific displays and buttons
            if (unit is AirUnit airUnit)
            {
                UpdateAirUnitDisplay(airUnit);
                
                // Show air unit buttons
                if (airUnit.HomeBaseId == -1) // In flight
                {
                    CircularLandButton.Visibility = Visibility.Visible;
                    CircularRTBButton.Visibility = Visibility.Visible;
                }
                
                if (unit is Bomber)
                {
                    CircularBombButton.Visibility = Visibility.Visible;
                }
            }
            else if (unit is Submarine submarine)
            {
                UpdateSubmarineDisplay(submarine);
                CircularSubmergeButton.Visibility = Visibility.Visible;
            }
            else if (unit is Carrier carrier)
            {
                UpdateCarrierDisplay(carrier);
            }
            else if (unit is Transport transport)
            {
                UpdateTransportDisplay(transport);
            }
            else if (unit is PatrolBoat patrolBoat)
            {
                UpdatePatrolBoatDisplay(patrolBoat);
            }
            else if (unit is Artillery artillery)
            {
                UpdateArtilleryDisplay(artillery);
            }
            else if (unit is AntiAircraft antiAircraft)
            {
                UpdateAntiAircraftDisplay(antiAircraft);
            }
            else if (unit is Spy spy)
            {
                UpdateSpyDisplay(spy);
            }
            else if (unit is Sapper sapper)
            {
                if (sapper.IsDisruptingSupply)
                {
                    CircularStopDisruptButton.Visibility = Visibility.Visible;
                    SapperBuildText.Visibility = Visibility.Visible;
                    SapperBuildText.Text = "🚫 Disrupting supply line (immobile, defenseless)";
                }
                else if (sapper.IsBuildingBase || sapper.IsBuildingBridge)
                {
                    CircularCancelBuildButton.Visibility = Visibility.Visible;
                    SapperBuildText.Visibility = Visibility.Visible;

                    string buildType = sapper.IsBuildingBase ? "Base" : "Bridge";
                    int turnsNeeded = sapper.IsBuildingBase ? 2 : 1;
                    int turnsRemaining = turnsNeeded - sapper.BuildProgress;
                    SapperBuildText.Text = $"🏗️ Building {buildType}: {turnsRemaining} turn(s) remaining";
                }
                else
                {
                    CircularBuildBaseButton.Visibility = Visibility.Visible;
                    CircularBuildBridgeButton.Visibility = Visibility.Visible;
                    CircularDisruptButton.Visibility = Visibility.Visible;
                }
            }
            else if (unit is Miner)
            {
                Tile minerTile = game.Map.GetTile(unit.Position);
                if (minerTile.Structure == null && ResourceRegistry.IsMineable(minerTile.Resource))
                    CircularBuildMineButton.Visibility = Visibility.Visible;
            }

            // Show Park button for land units on friendly structures
            if (unit is LandUnit)
            {
                Tile currentTile = game.Map.GetTile(unit.Position);
                if (currentTile.Structure != null &&
                    currentTile.Structure.OwnerId == game.CurrentPlayer.PlayerId &&
                    (currentTile.Structure is Base || currentTile.Structure is City))
                {
                    CircularParkButton.Visibility = Visibility.Visible;
                }
            }

            UpdateNextButton();
        }

        private void HideAllSpecializedButtons()
        {
            CircularLandButton.Visibility = Visibility.Collapsed;
            CircularRTBButton.Visibility = Visibility.Collapsed;
            CircularBombButton.Visibility = Visibility.Collapsed;
            CircularSubmergeButton.Visibility = Visibility.Collapsed;
            CircularParkButton.Visibility = Visibility.Collapsed;
            CircularBuildBaseButton.Visibility = Visibility.Collapsed;
            CircularBuildBridgeButton.Visibility = Visibility.Collapsed;
            CircularCancelBuildButton.Visibility = Visibility.Collapsed;
            CircularBuildMineButton.Visibility = Visibility.Collapsed;
            CircularDisruptButton.Visibility = Visibility.Collapsed;
            CircularStopDisruptButton.Visibility = Visibility.Collapsed;
        }

        private void UpdateUnitIcon(Unit unit)
        {
            // Map unit class names to icon file names (same mapping as MapRenderer)
            Dictionary<string, string> iconFileNameMap = new Dictionary<string, string>
            {
                {"Army", "Army"},
                {"Tank", "Tank"},
                {"Artillery", "Artillery"},
                {"AntiAircraft", "AntiAircraft"},
                {"Spy", "Spy"},
                {"Fighter", "Fighter"},
                {"Bomber", "Bomber"},
                {"Tanker", "Fighter"}, // Uses Fighter icon
                {"Carrier", "AircraftCarrier"},
                {"Battleship", "Battleship"},
                {"Destroyer", "Destroyer"},
                {"Submarine", "Submarine"},
                {"PatrolBoat", "PatrolBoat"},
                {"Transport", "PatrolBoat"}, // Uses PatrolBoat icon
                {"Sapper", "Sapper"}
            };

            string unitTypeName = unit.GetType().Name;
            string fileName = iconFileNameMap.ContainsKey(unitTypeName) ? iconFileNameMap[unitTypeName] : unitTypeName;

            // Build path to icon file (same structure as MapRenderer)
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string folderName = unit.IsVeteran ? "Veteran" : "Units";
            string iconPath =
                System.IO.Path.Combine(appDirectory, "Resources", "Empire_Icons", folderName, $"{fileName}.png");

            try
            {
                if (System.IO.File.Exists(iconPath))
                {
                    BitmapImage iconSource = new BitmapImage();
                    iconSource.BeginInit();
                    iconSource.UriSource = new Uri(iconPath, UriKind.Absolute);
                    iconSource.CacheOption = BitmapCacheOption.OnLoad;
                    iconSource.EndInit();
                    iconSource.Freeze();
                    UnitIconBrush.ImageSource = iconSource;
                }
                else
                {
                    // Fallback - try without Veteran/Units subfolder
                    string fallbackPath =
                        System.IO.Path.Combine(appDirectory, "Resources", "Empire_Icons", $"{fileName}.png");
                    if (System.IO.File.Exists(fallbackPath))
                    {
                        BitmapImage iconSource = new BitmapImage();
                        iconSource.BeginInit();
                        iconSource.UriSource = new Uri(fallbackPath, UriKind.Absolute);
                        iconSource.CacheOption = BitmapCacheOption.OnLoad;
                        iconSource.EndInit();
                        iconSource.Freeze();
                        UnitIconBrush.ImageSource = iconSource;
                    }
                    else
                    {
                        UnitIconBrush.ImageSource = null;
                        System.Diagnostics.Debug.WriteLine($"Unit icon not found: {iconPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnitIconBrush.ImageSource = null;
                System.Diagnostics.Debug.WriteLine($"Failed to load unit icon: {ex.Message}");
            }

            // Set player color border
            Color[] playerColors = new Color[]
            {
                Color.FromRgb(0, 120, 255), // Player 0 - Blue
                Color.FromRgb(255, 60, 60), // Player 1 - Red
                Color.FromRgb(60, 255, 60), // Player 2 - Green
                Color.FromRgb(255, 255, 60), // Player 3 - Yellow
                Color.FromRgb(255, 140, 0), // Player 4 - Orange
                Color.FromRgb(160, 60, 255), // Player 5 - Purple
                Color.FromRgb(0, 255, 255), // Player 6 - Cyan
                Color.FromRgb(255, 180, 200) // Player 7 - Pink
            };

            int playerId = unit.OwnerId;
            if (playerId >= 0 && playerId < playerColors.Length)
            {
                UnitIconBorder.BorderBrush = new SolidColorBrush(playerColors[playerId]);
            }
            else
            {
                UnitIconBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
            }
        }

        private void SelectStructure(Structure structure)
        {
            selectedStructure = structure;
            selectedUnit = null;

            UnitInfoPanel.Visibility = Visibility.Collapsed;
            StructureInfoPanel.Visibility = Visibility.Visible;

            StructureNameText.Text = structure.GetName();
            if (structure is Base || structure is City || structure is Mine)
                StructureNameText.Text += $"   👥 {structure.Population:0.#}";
            StructureLifeText.Text = $"Life: {structure.Life}/{structure.MaxLife}";

            // Color code life based on percentage
            double lifePercent = (double)structure.Life / structure.MaxLife;
            if (lifePercent > 0.7)
                StructureLifeText.Foreground = System.Windows.Media.Brushes.LimeGreen;
            else if (lifePercent > 0.4)
                StructureLifeText.Foreground = System.Windows.Media.Brushes.Orange;
            else
                StructureLifeText.Foreground = System.Windows.Media.Brushes.Red;

            if (structure is Base baseStructure)
            {
                StructureFacilitiesPanel.Visibility = Visibility.Visible;
                MineActionsPanel.Visibility = Visibility.Collapsed;
                DefenseBonusText.Text = $"Defense Bonus: +{baseStructure.GetDefenseBonus()}";
                UpdateStructureLists(baseStructure);
                UpdateProductionQueue(baseStructure);
                PopulateAvailableUnits(baseStructure);
                RefreshCivicUpgrades(baseStructure);

                // Show/hide shipyard based on HasShipyard flag
                if (baseStructure.HasShipyard)
                {
                    ShipyardLabel.Visibility = Visibility.Visible;
                    ShipyardCapacityText.Visibility = Visibility.Visible;
                    ShipyardList.Visibility = Visibility.Visible;
                    LaunchShipButton.Visibility = Visibility.Visible;
                    RepairShipButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ShipyardLabel.Visibility = Visibility.Collapsed;
                    ShipyardCapacityText.Visibility = Visibility.Collapsed;
                    ShipyardList.Visibility = Visibility.Collapsed;
                    LaunchShipButton.Visibility = Visibility.Collapsed;
                    RepairShipButton.Visibility = Visibility.Collapsed;
                }
            }
            else if (structure is City city)
            {
                StructureFacilitiesPanel.Visibility = Visibility.Visible;
                MineActionsPanel.Visibility = Visibility.Collapsed;
                DefenseBonusText.Text = $"Defense Bonus: +{city.GetDefenseBonus()}";
                UpdateStructureLists(city);
                UpdateProductionQueue(city);
                PopulateAvailableUnits(city);
                RefreshCivicUpgrades(city);

                // Cities don't have shipyards
                ShipyardLabel.Visibility = Visibility.Collapsed;
                ShipyardCapacityText.Visibility = Visibility.Collapsed;
                ShipyardList.Visibility = Visibility.Collapsed;
                LaunchShipButton.Visibility = Visibility.Collapsed;
                RepairShipButton.Visibility = Visibility.Collapsed;
            }
            else if (structure is Mine mine)
            {
                // Mines have no facilities and can only produce Miners.
                StructureFacilitiesPanel.Visibility = Visibility.Collapsed;
                MineActionsPanel.Visibility = Visibility.Visible;
                DefenseBonusText.Text = $"Defense Bonus: +{mine.GetDefenseBonus()}";

                var def = ResourceRegistry.Get(mine.Resource);
                MineInfoText.Text = $"{def.DisplayName} mine — {(mine.IsConnected ? "supply line connected" : "supply line CUT")}.\n" +
                                    $"Populace: {mine.Population:0.#}   (Miner costs 👥2)";

                int minerGold = UnitProductionOrder.GetCost(typeof(Miner)).GetValueOrDefault(ResourceType.Gold);
                BuildMinerFromMineButton.IsEnabled =
                    mine.OwnerId == game.CurrentPlayer.PlayerId &&
                    mine.Population - UnitProductionOrder.PopulationCost(typeof(Miner)) >= 1 &&
                    game.CurrentPlayer.GetResource(ResourceType.Gold) >= minerGold;
            }

            UpdateNextButton();
        }


        private void UpdateStructureLists(Base baseStructure)
        {
            // Update Airport
            AirportList.Items.Clear();
            int airportUsed = baseStructure.GetAirportSpaceUsed();
            AirportCapacityText.Text = $"Capacity: {airportUsed}/{Base.MAX_AIRPORT_CAPACITY}";
            if (airportUsed >= Base.MAX_AIRPORT_CAPACITY)
            {
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.LightBlue;
            }

            foreach (AirUnit aircraft in baseStructure.Airport)
            {
                string status = "";
                if (baseStructure.UnitsBeingRepaired.ContainsKey(aircraft))
                {
                    int turnsLeft = baseStructure.UnitsBeingRepaired[aircraft];
                    status = $" [Repairing: {turnsLeft} turn(s)]";
                }
                AirportList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}/{aircraft.MaxFuel}, Life: {aircraft.Life}/{aircraft.MaxLife}{status}");
            }

            // Update Shipyard
            ShipyardList.Items.Clear();
            if (baseStructure.HasShipyard)
            {
                int shipyardUsed = baseStructure.GetShipyardSpaceUsed();
                ShipyardCapacityText.Text = $"Capacity: {shipyardUsed}/{Base.MAX_SHIPYARD_CAPACITY}";
                if (shipyardUsed >= Base.MAX_SHIPYARD_CAPACITY)
                {
                    ShipyardCapacityText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    ShipyardCapacityText.Foreground = System.Windows.Media.Brushes.LightBlue;
                }

                foreach (SeaUnit ship in baseStructure.Shipyard)
                {
                    string status = "";
                    if (baseStructure.UnitsBeingRepaired.ContainsKey(ship))
                    {
                        int turnsLeft = baseStructure.UnitsBeingRepaired[ship];
                        status = $" [Repairing: {turnsLeft} turn(s)]";
                    }
                    ShipyardList.Items.Add($"{ship.GetName()} - Life: {ship.Life}/{ship.MaxLife}{status}");
                }
            }

            // Update Motor Pool
            MotorPoolList.Items.Clear();
            foreach (Unit unit in baseStructure.MotorPool)
            {
                MotorPoolList.Items.Add($"{unit.GetName()} - Life: {unit.Life}/{unit.MaxLife}");
            }

            // Update Barracks
            BarracksList.Items.Clear();
            int barracksUsed = baseStructure.GetBarracksSpaceUsed();
            BarracksCapacityText.Text = $"Capacity: {barracksUsed}/{Base.MAX_BARRACKS_CAPACITY}";
            if (barracksUsed >= Base.MAX_BARRACKS_CAPACITY)
            {
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.LightBlue;
            }

            foreach (LandUnit army in baseStructure.Barracks)
            {
                BarracksList.Items.Add($"{army.GetName()} - Life: {army.Life}/{army.MaxLife}");
            }
        }

        private void UpdateStructureLists(City city)
        {
            // Update Airport
            AirportList.Items.Clear();
            int airportUsed = city.GetAirportSpaceUsed();
            AirportCapacityText.Text = $"Capacity: {airportUsed}/{City.MAX_AIRPORT_CAPACITY}";
            if (airportUsed >= City.MAX_AIRPORT_CAPACITY)
            {
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.LightBlue;
            }

            foreach (AirUnit aircraft in city.Airport)
            {
                string status = "";
                if (city.UnitsBeingRepaired.ContainsKey(aircraft))
                {
                    int turnsLeft = city.UnitsBeingRepaired[aircraft];
                    status = $" [Repairing: {turnsLeft} turn(s)]";
                }
                AirportList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}/{aircraft.MaxFuel}, Life: {aircraft.Life}/{aircraft.MaxLife}{status}");
            }

            // Update Motor Pool
            MotorPoolList.Items.Clear();
            foreach (Unit unit in city.MotorPool)
            {
                MotorPoolList.Items.Add($"{unit.GetName()} - Life: {unit.Life}/{unit.MaxLife}");
            }

            // Update Barracks
            BarracksList.Items.Clear();
            int barracksUsed = city.GetBarracksSpaceUsed();
            BarracksCapacityText.Text = $"Capacity: {barracksUsed}/{City.MAX_BARRACKS_CAPACITY}";
            if (barracksUsed >= City.MAX_BARRACKS_CAPACITY)
            {
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.LightBlue;
            }

            foreach (LandUnit army in city.Barracks)
            {
                BarracksList.Items.Add($"{army.GetName()} - Life: {army.Life}/{army.MaxLife}");
            }
        }


        private void AirportList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AirUnit? aircraft = GetSelectedAircraft();
            TakeOffButton.IsEnabled = aircraft != null && !IsBeingRepaired(aircraft);
            CircularBombButton.IsEnabled = aircraft is Bomber && !IsBeingRepaired(aircraft);
            RepairAircraftButton.IsEnabled = aircraft != null && aircraft.Life < aircraft.MaxLife && !IsBeingRepaired(aircraft);
        }

        private void ShipyardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SeaUnit? ship = GetSelectedShip();
            LaunchShipButton.IsEnabled = ship != null && !IsBeingRepaired(ship);
            RepairShipButton.IsEnabled = ship != null && ship.Life < ship.MaxLife && !IsBeingRepaired(ship);
        }

        private SeaUnit GetSelectedShip()
        {
            if (ShipyardList.SelectedIndex < 0 || selectedStructure == null)
                return null;

            if (selectedStructure is Base baseStructure)
            {
                if (ShipyardList.SelectedIndex < baseStructure.Shipyard.Count)
                    return baseStructure.Shipyard[ShipyardList.SelectedIndex];
            }

            return null;
        }

        private bool IsBeingRepaired(Unit unit)
        {
            if (selectedStructure is Base baseStructure)
            {
                return baseStructure.UnitsBeingRepaired.ContainsKey(unit);
            }
            else if (selectedStructure is City city)
            {
                return city.UnitsBeingRepaired.ContainsKey(unit);
            }
            return false;
        }

        private void RepairAircraftButton_Click(object sender, RoutedEventArgs e)
        {
            AirUnit? aircraft = GetSelectedAircraft();
            if (aircraft == null || selectedStructure == null)
                return;

            int damageToRepair = aircraft.MaxLife - aircraft.Life;

            if (damageToRepair == 0)
            {
                AddMessage("Unit is already at full health!", MessageType.Info);
                return;
            }

            if (selectedStructure is Base baseStructure)
            {
                baseStructure.UnitsBeingRepaired[aircraft] = damageToRepair;
            }
            else if (selectedStructure is City city)
            {
                city.UnitsBeingRepaired[aircraft] = damageToRepair;
            }

            AddMessage($"Repair started. Will take {damageToRepair} turn(s).", MessageType.Production);
            SelectStructure(selectedStructure);
        }

        private void RepairShipButton_Click(object sender, RoutedEventArgs e)
        {
            SeaUnit? ship = GetSelectedShip();
            if (ship == null || selectedStructure == null)
                return;

            int damageToRepair = ship.MaxLife - ship.Life;

            if (damageToRepair == 0)
            {
                MessageDialog.Show(this, "Unit is already at full health!", "Repair");
                return;
            }

            // Start repair (1 turn per point of damage)
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.UnitsBeingRepaired[ship] = damageToRepair;
            }

            MessageDialog.Show(this, $"Repair started. Will take {damageToRepair} turn(s).", "Repair");
            SelectStructure(selectedStructure);
        }

        private void LaunchShipButton_Click(object sender, RoutedEventArgs e)
        {
            SeaUnit? ship = GetSelectedShip();
            if (ship == null || selectedStructure == null)
                return;

            TilePosition adjacentPos = FindAdjacentWaterTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                AddMessage("No water to launch into!", MessageType.Warning);
                return;
            }

            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Shipyard.Remove(ship);
            }

            ship.Position = adjacentPos;

            Tile tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(ship);

            AddMessage($"{ship.GetName()} launched from {selectedStructure.GetName()}", MessageType.Success);

            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private TilePosition FindAdjacentWaterTile(TilePosition centerPos)
        {
            int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
            int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

            for (int i = 0; i < 8; i++)
            {
                TilePosition pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

                if (game.Map.IsValidPosition(pos))
                {
                    Tile tile = game.Map.GetTile(pos);

                    if (tile.Units.Count == 0 &&
                        tile.Structure == null &&
                        (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater))
                    {
                        return pos;
                    }
                }
            }

            return new TilePosition(-1, -1);
        }
        private void MotorPoolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeployMotorButton.IsEnabled = MotorPoolList.SelectedIndex >= 0;
        }

        private void BarracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeployArmyButton.IsEnabled = BarracksList.SelectedIndex >= 0;
        }

        private AirUnit GetSelectedAircraft()
        {
            if (AirportList.SelectedIndex < 0 || selectedStructure == null)
                return null;

            if (selectedStructure is Base baseStructure)
            {
                if (AirportList.SelectedIndex < baseStructure.Airport.Count)
                    return baseStructure.Airport[AirportList.SelectedIndex];
            }
            else if (selectedStructure is City city)
            {
                if (AirportList.SelectedIndex < city.Airport.Count)
                    return city.Airport[AirportList.SelectedIndex];
            }

            return null;
        }

        private Unit GetSelectedMotorUnit()
        {
            if (MotorPoolList.SelectedIndex < 0 || selectedStructure == null)
                return null;

            if (selectedStructure is Base baseStructure)
            {
                if (MotorPoolList.SelectedIndex < baseStructure.MotorPool.Count)
                    return baseStructure.MotorPool[MotorPoolList.SelectedIndex];
            }
            else if (selectedStructure is City city)
            {
                if (MotorPoolList.SelectedIndex < city.MotorPool.Count)
                    return city.MotorPool[MotorPoolList.SelectedIndex];
            }

            return null;
        }

        private LandUnit GetSelectedArmyUnit()
        {
            if (BarracksList.SelectedIndex < 0 || selectedStructure == null)
                return null;

            if (selectedStructure is Base baseStructure)
            {
                if (BarracksList.SelectedIndex < baseStructure.Barracks.Count)
                    return baseStructure.Barracks[BarracksList.SelectedIndex];
            }
            else if (selectedStructure is City city)
            {
                if (BarracksList.SelectedIndex < city.Barracks.Count)
                    return city.Barracks[BarracksList.SelectedIndex];
            }

            return null;
        }


        private void TakeOffButton_Click(object sender, RoutedEventArgs e)
        {
            AirUnit? aircraft = GetSelectedAircraft();
            if (aircraft == null || selectedStructure == null)
                return;

            TilePosition adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                AddMessage("No airspace to take off into!", MessageType.Warning);
                return;
            }

            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Airport.Remove(aircraft);
            }
            else if (selectedStructure is City city)
            {
                city.Airport.Remove(aircraft);
            }

            aircraft.Position = adjacentPos;
            aircraft.HomeBaseId = -1;
            aircraft.Fuel = aircraft.MaxFuel;

            Tile tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(aircraft);

            AddMessage($"{aircraft.GetName()} took off from {selectedStructure.GetName()}", MessageType.Movement);

            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void DeployMotorButton_Click(object sender, RoutedEventArgs e)
        {
            Unit? unit = GetSelectedMotorUnit();
            if (unit == null || selectedStructure == null)
                return;

            // Find an adjacent empty tile
            TilePosition adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageDialog.Warn(this, "No space to deploy unit!", "Deploy");
                return;
            }

            // Remove from motor pool
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.MotorPool.Remove(unit);
            }
            else if (selectedStructure is City city)
            {
                city.MotorPool.Remove(unit);
            }

            // Place on map
            unit.Position = adjacentPos;

            Tile tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(unit);

            // Update display
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void DeployArmyButton_Click(object sender, RoutedEventArgs e)
        {
            LandUnit? unit = GetSelectedArmyUnit();
            if (unit == null || selectedStructure == null)
                return;

            // Find an adjacent empty tile
            TilePosition adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageDialog.Warn(this, "No space to deploy unit!", "Deploy");
                return;
            }

            // Remove from barracks
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Barracks.Remove(unit);
            }
            else if (selectedStructure is City city)
            {
                city.Barracks.Remove(unit);
            }

            // Place on map
            unit.Position = adjacentPos;

            Tile tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(unit);

            // Update display
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void BombingRunButton_Click(object sender, RoutedEventArgs e)
        {
            Bomber? bomber = GetSelectedAircraft() as Bomber;
            if (bomber == null)
                return;

            // First deploy the bomber to an adjacent tile
            TilePosition adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageDialog.Warn(this, "No airspace to take off into!", "Launch");
                return;
            }

            // Remove from airport
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Airport.Remove(bomber);
            }
            else if (selectedStructure is City city)
            {
                city.Airport.Remove(bomber);
            }

            // Place on map temporarily
            bomber.Position = adjacentPos;
            bomber.HomeBaseId = -1;
            bomber.Fuel = bomber.MaxFuel;

            Tile tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(bomber);

            // Now set up bombing run UI
            isSelectingBomberTarget = true;
            bomberForMission = bomber;
            MapCanvas.Cursor = Cursors.Cross;

            BomberMissionPanel.Visibility = Visibility.Visible;
            BomberRangeText.Text = $"Bomber Range: {bomber.MaxFuel / 2} tiles (round trip)";

            // Populate available escorts from the airport
            AvailableEscortsList.Items.Clear();
            if (selectedStructure is Base baseStruct)
            {
                foreach (AirUnit aircraft in baseStruct.Airport)
                {
                    if (aircraft is Fighter)
                    {
                        AvailableEscortsList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}");
                    }
                }

                // Check for tankers
                List<AirUnit> tankers = baseStruct.Airport.Where(a => a is Tanker).ToList();
                if (tankers.Count > 0)
                {
                    IncludeTankerCheckbox.IsEnabled = true;
                    AvailableTankerText.Text = $"({tankers.Count} tanker(s) available)";
                }
                else
                {
                    IncludeTankerCheckbox.IsEnabled = false;
                    AvailableTankerText.Text = "(No tankers available)";
                }
            }
            else if (selectedStructure is City cityStruct)
            {
                foreach (AirUnit aircraft in cityStruct.Airport)
                {
                    if (aircraft is Fighter)
                    {
                        AvailableEscortsList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}");
                    }
                }

                List<AirUnit> tankers = cityStruct.Airport.Where(a => a is Tanker).ToList();
                if (tankers.Count > 0)
                {
                    IncludeTankerCheckbox.IsEnabled = true;
                    AvailableTankerText.Text = $"({tankers.Count} tanker(s) available)";
                }
                else
                {
                    IncludeTankerCheckbox.IsEnabled = false;
                    AvailableTankerText.Text = "(No tankers available)";
                }
            }

            MessageDialog.Show(this, "Select a target on the map for the bombing run.", "Bombing Run");

            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private TilePosition FindAdjacentEmptyTile(TilePosition centerPos)
        {
            // Check all adjacent tiles
            int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 }; // Including diagonals
            int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

            for (int i = 0; i < 8; i++)
            {
                TilePosition pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

                if (game.Map.IsValidPosition(pos))
                {
                    Tile tile = game.Map.GetTile(pos);

                    // Check if tile is empty and has valid terrain
                    if (tile.Units.Count == 0 &&
                        tile.Structure == null &&
                        (tile.Terrain == TerrainType.Land ||
                         tile.Terrain == TerrainType.Plains ||
                         tile.Terrain == TerrainType.Forest ||
                         tile.Terrain == TerrainType.Hills))
                    {
                        return pos;
                    }
                }
            }

            return new TilePosition(-1, -1); // No empty tile found
        }

        private void UpdateProductionQueue(Base baseStructure)
        {
            ProductionQueueList.Items.Clear();
            int index = 0;
            foreach (UnitProductionOrder order in baseStructure.ProductionQueue)
            {
                if (index == 0)
                {
                    // First item - show actual progress
                    ProductionQueueList.Items.Add($"▶ {order.DisplayName} ({(int)baseStructure.CurrentProductionProgress}/{order.TotalCost})");
                }
                else
                {
                    // Items in queue - show waiting
                    ProductionQueueList.Items.Add($"  {order.DisplayName} (Queued - {order.TotalCost} pts)");
                }
                index++;
            }
        }

        private void UpdateProductionQueue(City city)
        {
            ProductionQueueList.Items.Clear();
            int index = 0;
            foreach (UnitProductionOrder order in city.ProductionQueue)
            {
                if (index == 0)
                {
                    // First item - show actual progress
                    ProductionQueueList.Items.Add($"▶ {order.DisplayName} ({(int)city.CurrentProductionProgress}/{order.TotalCost})");
                }
                else
                {
                    // Items in queue - show waiting
                    ProductionQueueList.Items.Add($"  {order.DisplayName} (Queued - {order.TotalCost} pts)");
                }
                index++;
            }
        }

        private void PopulateAvailableUnits(Structure structure)
        {
            UnitTypesCombo.Items.Clear();

            Base? baseStructure = structure as Base;
            City? city = structure as City;
            Player player = game.CurrentPlayer;

            // Build the "(💰3 ⚙️2)" cost fragment and affordability flag from a cost map,
            // iterating whatever resources the registry defines.
            (bool canAfford, string costText) DescribeCost(Dictionary<ResourceType, int> cost)
            {
                bool afford = true;
                var sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var rt in ResourceRegistry.Currencies)
                {
                    int amt = cost.TryGetValue(rt, out var v) ? v : 0;
                    if (amt <= 0) continue;
                    bool ok = player.GetResource(rt) >= amt;
                    if (!ok) afford = false;
                    if (!first) sb.Append(' ');
                    first = false;
                    sb.Append(ResourceRegistry.Get(rt).Symbol).Append(amt);
                    if (!ok) sb.Append("[!]");
                }
                return (afford, sb.ToString());
            }

            void AddUnit(string name, Type type)
            {
                var cost = UnitProductionOrder.GetCost(type);
                bool canBuild = false;
                string capacityNote = "";

                // Check capacity
                if (baseStructure != null)
                {
                    canBuild = baseStructure.CanBuildUnit(type);

                    if (type == typeof(Fighter) || type == typeof(Bomber) || type == typeof(Tanker))
                        capacityNote = $" [{baseStructure.GetAirportSpaceUsed()}/{Base.MAX_AIRPORT_CAPACITY}]";
                    else if (type == typeof(Carrier) || type == typeof(Battleship) ||
                             type == typeof(Destroyer) || type == typeof(Submarine) ||
                             type == typeof(PatrolBoat) || type == typeof(Transport))
                        capacityNote = $" [{baseStructure.GetShipyardSpaceUsed()}/{Base.MAX_SHIPYARD_CAPACITY}]";
                    else if (type == typeof(Army))
                        capacityNote = $" [{baseStructure.GetBarracksSpaceUsed()}/{Base.MAX_BARRACKS_CAPACITY}]";
                }
                else if (city != null)
                {
                    canBuild = city.CanBuildUnit(type);

                    if (type == typeof(Fighter) || type == typeof(Bomber) || type == typeof(Tanker))
                        capacityNote = $" [{city.GetAirportSpaceUsed()}/{City.MAX_AIRPORT_CAPACITY}]";
                    else if (type == typeof(Army))
                        capacityNote = $" [{city.GetBarracksSpaceUsed()}/{City.MAX_BARRACKS_CAPACITY}]";
                }

                // Check resources / build cost string (dynamic over the registry)
                var (canAfford, costText) = DescribeCost(cost);

                // People-units also consume populace from this structure (never below 1).
                bool popOk = true;
                string popNote = "";
                int popCost = UnitProductionOrder.PopulationCost(type);
                if (popCost > 0)
                {
                    double pop = baseStructure?.Population ?? city?.Population ?? 0;
                    popOk = pop - popCost >= 1;
                    popNote = popOk ? $" 👥{popCost}" : $" 👥{popCost}[!]";
                }

                string costString = $"{name} ({costText}{popNote})";

                if (!canAfford)
                    costString += " [Need Resources]";
                else if (!popOk)
                    costString += " [Need Populace]";
                else if (canBuild)
                    costString += " ✓";

                costString += capacityNote;

                ComboBoxItem item = new ComboBoxItem
                {
                    Content = costString,
                    Tag = new UnitProductionOrder(type, name),
                    IsEnabled = canBuild && canAfford && popOk
                };

                // Color coding
                if (!canAfford || !popOk)
                    item.Foreground = System.Windows.Media.Brushes.Red;
                else if (canBuild)
                    item.Foreground = System.Windows.Media.Brushes.LimeGreen;

                UnitTypesCombo.Items.Add(item);
            }

            void AddSatelliteUnit(string name, Type type, OrbitType orbitType)
            {
                var cost = UnitProductionOrder.GetCost(type);

                // Satellites can be built at bases and cities (no capacity limit)
                bool canBuild = (baseStructure != null || city != null);

                // Check resources / build cost string (dynamic over the registry)
                var (canAfford, costText) = DescribeCost(cost);
                string costString = $"{name} ({costText})";

                if (!canAfford)
                    costString += " [Need Resources]";
                else if (canBuild)
                    costString += " ✓";

                ComboBoxItem item = new ComboBoxItem
                {
                    Content = costString,
                    Tag = new SatelliteProductionOrder(type, name, orbitType),
                    IsEnabled = canBuild && canAfford
                };

                // Color coding
                if (!canAfford)
                    item.Foreground = System.Windows.Media.Brushes.Red;
                else if (canBuild)
                    item.Foreground = System.Windows.Media.Brushes.LimeGreen;

                UnitTypesCombo.Items.Add(item);
            }

            // Add all units (costs come from UnitProductionOrder.Costs)
            AddUnit("Army", typeof(Army));
            AddUnit("Miner", typeof(Miner));
            AddUnit("Tank", typeof(Tank));
            AddUnit("Artillery", typeof(Artillery));
            AddUnit("Sapper", typeof(Sapper));
            AddUnit("Anti-Aircraft", typeof(AntiAircraft));
            AddUnit("Spy", typeof(Spy));
            AddUnit("Fighter", typeof(Fighter));
            AddUnit("Bomber", typeof(Bomber));
            AddUnit("Tanker", typeof(Tanker));

            // Add Orbiting Satellites - one entry for each orbit type that player hasn't deployed yet
            if (!player.DeployedOrbitTypes.Contains(OrbitType.Horizontal))
                AddSatelliteUnit("Orbit Sat (Horizontal)", typeof(OrbitingSatellite), OrbitType.Horizontal);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.Vertical))
                AddSatelliteUnit("Orbit Sat (Vertical)", typeof(OrbitingSatellite), OrbitType.Vertical);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.RightDiagonal))
                AddSatelliteUnit("Orbit Sat (Right Diag)", typeof(OrbitingSatellite), OrbitType.RightDiagonal);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.LeftDiagonal))
                AddSatelliteUnit("Orbit Sat (Left Diag)", typeof(OrbitingSatellite), OrbitType.LeftDiagonal);

            // Add Geosynchronous Satellite (no restrictions)
            AddUnit("Geosync Satellite", typeof(GeosynchronousSatellite));

            if (baseStructure != null && baseStructure.HasShipyard)
            {
                AddUnit("Patrol Boat", typeof(PatrolBoat));
                AddUnit("Destroyer", typeof(Destroyer));
                AddUnit("Submarine", typeof(Submarine));
                AddUnit("Carrier", typeof(Carrier));
                AddUnit("Battleship", typeof(Battleship));
                AddUnit("Transport", typeof(Transport));
            }
        }

        private void ClearSelection()
        {
            selectedUnit = null;
            selectedStructure = null;
            UnitInfoPanel.Visibility = Visibility.Collapsed;
            StructureInfoPanel.Visibility = Visibility.Collapsed;
            TileInfoPanel.Visibility = Visibility.Collapsed;  
            CircularParkButton.Visibility = Visibility.Collapsed;
        }

        private void SelectTile(TilePosition tilePos)
        {
            selectedUnit = null;
            selectedStructure = null;

            UnitInfoPanel.Visibility = Visibility.Collapsed;
            StructureInfoPanel.Visibility = Visibility.Collapsed;
            TileInfoPanel.Visibility = Visibility.Visible;

            Tile tile = game.Map.GetTile(tilePos);

            // Display terrain information
            string terrainName = GetTerrainDisplayName(tile.Terrain);
            TileTerrainNameText.Text = terrainName;

            // Movement cost for land units (most common reference)
            double movementCost = tile.GetMovementCost(new Army { OwnerId = 0, Position = tilePos });
            if (movementCost == double.MaxValue)
            {
                TileMovementCostText.Text = "Impassable (Land)";
                TileMovementCostText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TileMovementCostText.Text = $"{movementCost} Movement Point{(movementCost == 1.0 ? "" : "s")}";
                TileMovementCostText.Foreground = System.Windows.Media.Brushes.LightGray;
            }

            // Defense bonus
            double defenseBonus = tile.GetDefenseBonus(new Army { OwnerId = 0, Position = tilePos });
            if (defenseBonus > 1.0)
            {
                TileDefenseBonusText.Text = $"+{((defenseBonus - 1.0) * 100):F0}% Defense Bonus";
                TileDefenseBonusText.Foreground = System.Windows.Media.Brushes.LightBlue;
                TileDefenseBonusText.Visibility = Visibility.Visible;
            }
            else
            {
                TileDefenseBonusText.Visibility = Visibility.Collapsed;
            }

            // Resources
            if (tile.Resource != ResourceType.None)
            {
                TileResourcePanel.Visibility = Visibility.Visible;
                string resourceName = tile.Resource == ResourceType.Oil ? "Oil" : "Steel";
                TileResourceText.Text = $"⚡ {resourceName} Resource (+1 per turn)";
            }
            else
            {
                TileResourcePanel.Visibility = Visibility.Collapsed;
            }

            // Ownership
            if (tile.OwnerId >= 0 && tile.OwnerId < game.Players.Count)
            {
                Player owner = game.Players[tile.OwnerId];
                TileOwnerText.Text = $"Controlled by: {owner.Name}";
                TileOwnerText.Foreground = GetPlayerBrush(tile.OwnerId);
                TileOwnerText.Visibility = Visibility.Visible;
            }
            else
            {
                TileOwnerText.Text = "Neutral Territory";
                TileOwnerText.Foreground = System.Windows.Media.Brushes.Gray;
                TileOwnerText.Visibility = Visibility.Visible;
            }

            // Bridge info
            if (tile.HasBridge)
            {
                TileBridgeText.Text = $"🌉 {tile.BridgeName}";
                TileBridgeText.Visibility = Visibility.Visible;
            }
            else
            {
                TileBridgeText.Visibility = Visibility.Collapsed;
            }

            // Enemy units on this tile
            List<Unit> enemyUnits = tile.Units.Where(u => u.OwnerId != game.CurrentPlayer.PlayerId && !(u is Satellite)).ToList();

            if (enemyUnits.Count > 0)
            {
                EnemyUnitsPanel.Visibility = Visibility.Visible;
                EnemyUnitsList.Items.Clear();

                foreach (Unit unit in enemyUnits)
                {
                    Player owner = game.Players[unit.OwnerId];
                    string unitInfo = $"{unit.GetName()} ({owner.Name})";

                    // Show life if visible
                    VisibilityLevel visibility = game.CurrentPlayer.FogOfWar.ContainsKey(tilePos)
                        ? game.CurrentPlayer.FogOfWar[tilePos]
                        : VisibilityLevel.Hidden;

                    if (visibility == VisibilityLevel.Visible)
                    {
                        unitInfo += $" - Life: {unit.Life}/{unit.MaxLife}";
                        if (unit.IsVeteran)
                            unitInfo += " ⭐";
                    }

                    EnemyUnitsList.Items.Add(unitInfo);
                }
            }
            else
            {
                EnemyUnitsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private string GetTerrainDisplayName(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Ocean => "Deep Ocean",
                TerrainType.CoastalWater => "Coastal Water",
                TerrainType.Land => "Grassland",
                TerrainType.Plains => "Plains",
                TerrainType.Forest => "Forest",
                TerrainType.Hills => "Hills",
                TerrainType.Mountain => "Mountain",
                _ => terrain.ToString()
            };
        }

        private System.Windows.Media.Brush GetPlayerBrush(int playerId)
        {
            Color[] playerColors = new Color[]
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

            if (playerId < 0 || playerId >= playerColors.Length)
                return System.Windows.Media.Brushes.Gray;

            Color color = playerColors[playerId];
            return new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        }


        private async void MoveUnit(Unit unit, TilePosition destination)
        {
            if (unit.Position.X < 0)
                return; // unit is in storage; deploy it from the structure panel first

            if (!game.Map.IsValidPosition(destination))
                return;

            Tile destinationTile = game.Map.GetTile(destination);

            // Military units destroy an enemy mine by attacking it (Miners capture it instead,
            // handled by the normal-move/capture path below).
            if (destinationTile.Structure is Mine enemyMine &&
                enemyMine.OwnerId != game.CurrentPlayer.PlayerId && !(unit is Miner))
            {
                int mdx = Math.Abs(unit.Position.X - destination.X);
                int mdy = Math.Abs(unit.Position.Y - destination.Y);
                if (Math.Max(mdx, mdy) > 1)
                {
                    AddMessage("Move adjacent to the mine to attack it.", MessageType.Info);
                    return;
                }
                if (unit.Attack <= 0)
                {
                    AddMessage("This unit can't attack a mine.", MessageType.Warning);
                    return;
                }

                game.AttackStructure(unit, enemyMine);
                unit.MovementPoints = 0;
                if (enemyMine.Life <= 0)
                {
                    game.RemoveDestroyedMines();
                    AddMessage("💥 Enemy mine destroyed!", MessageType.Success);
                }
                else
                {
                    AddMessage($"Mine attacked — {enemyMine.Life}/{enemyMine.MaxLife} HP remaining.", MessageType.Info);
                }
                game.UpdateSupplyLines();
                SelectUnit(unit);
                game.CurrentPlayer.UpdateVision(game.Map);
                UpdateResourceDisplay();
                RenderMap();
                return;
            }

            // Check if destination has an enemy unit
            Unit? enemyUnit = destinationTile.Units.FirstOrDefault(u => u.OwnerId != game.CurrentPlayer.PlayerId);

            double movementCost = 0;

            if (enemyUnit != null)
            {
                // Check if artillery has already attacked this turn
                if (unit is Artillery artilleryUnit)
                {
                    if (artilleryUnit.HasAttackedThisTurn)
                    {
                        AddMessage("Artillery can only attack once per turn!", MessageType.Warning);
                        return;
                    }
                }

                // Check if unit has enough movement points to attack
                movementCost = destinationTile.GetMovementCost(unit);
                if (unit.MovementPoints < movementCost)
                {
                    AddMessage($"Not enough movement points to attack! Need {movementCost}, have {unit.MovementPoints:F1}", MessageType.Warning);
                    return;
                }

                // COMBAT!
                TilePosition originalPosition = unit.Position;

                // Reveal spy if attacking
                if (unit is Spy attackingSpy && !attackingSpy.IsRevealed)
                {
                    attackingSpy.IsRevealed = true;
                    AddMessage("🎭 Spy's disguise blown! Now revealed to all enemies.", MessageType.Warning);
                }

                // Calculate combat result
                CombatResult combatResult = game.CalculateCombat(unit, enemyUnit, originalPosition);

                // Mark artillery as having attacked
                if (unit is Artillery artilleryAttacker)
                {
                    artilleryAttacker.HasAttackedThisTurn = true;
                }

                // Show combat window
                CombatWindow combatWindow = new CombatWindow(combatResult);
                combatWindow.Owner = this;
                bool? result = combatWindow.ShowDialog();

                if (combatWindow.AttackerRetreated)
                {
                    // Attacker retreated - stays in original position with reduced health
                    SelectUnit(unit);

                    string unitName = unit.GetName().ToLower();
                    string enemyName = enemyUnit.GetName().ToLower();
                    if (unit.IsVeteran)
                        unitName = "veteran " + unitName;
                    if (enemyUnit.IsVeteran)
                        enemyName = "veteran " + enemyName;

                    AddMessage($"Your {unitName} retreated from combat with {enemyName}!");
                }
                else if (combatResult.DefenderWon)
                {
                    // Attacker was destroyed — remove it from the tile and player's unit list immediately
                    Tile startTile = game.Map.GetTile(unit.Position);
                    startTile.Units.Remove(unit);
                    game.CurrentPlayer.Units.Remove(unit);
                    game.CurrentPlayer.Statistics.UnitsLost++;

                    ClearSelection();

                    string unitName = unit.GetName().ToLower();
                    string enemyName = enemyUnit.GetName().ToLower();
                    if (unit.IsVeteran)
                        unitName = "veteran " + unitName;
                    if (enemyUnit.IsVeteran)
                        enemyName = "veteran " + enemyName;

                    AddMessage($"⚔️ Your {unitName} was destroyed by {enemyName}!", MessageType.Combat);

                    game.CurrentPlayer.UpdateVision(game.Map);
                    RenderMap();
                    return;
                }
                else if (combatResult.AttackerWon)
                {
                    // Check if this was an artillery ranged attack - if so, attacker stays in place
                    int attackDistance = Math.Abs(originalPosition.X - destination.X) +
                                         Math.Abs(originalPosition.Y - destination.Y);
                    bool wasArtilleryRangedAttack = unit is Artillery && attackDistance > 1;

                    // Remove dead defender
                    destinationTile.Units.Remove(enemyUnit);
                    Player? defender = game.Players.FirstOrDefault(p => p.PlayerId == enemyUnit.OwnerId);
                    if (defender != null)
                    {
                        defender.Units.Remove(enemyUnit);
                        defender.RecordUnitLoss(unit.OwnerId);
                    }

                    // Track kill for human player
                    game.CurrentPlayer.RecordEnemyKill();

                    if (!wasArtilleryRangedAttack)
                    {
                        // Melee attacker advances to the destination tile
                        Tile startTile = game.Map.GetTile(unit.Position);
                        startTile.Units.Remove(unit);

                        unit.Position = destination;
                        destinationTile.Units.Add(unit);
                        destinationTile.OwnerId = unit.OwnerId;

                        // Check for structure capture
                        if (destinationTile.Structure != null && destinationTile.Structure.OwnerId != unit.OwnerId)
                        {
                            Structure capturedStructure = destinationTile.Structure;
                            Player? oldOwner = game.Players.FirstOrDefault(p => p.PlayerId == capturedStructure.OwnerId);
                            if (oldOwner != null)
                            {
                                oldOwner.RecordStructureLoss(capturedStructure);
                                oldOwner.Structures.Remove(capturedStructure);
                                AddMessage($"⚠️ {oldOwner.Name} lost {capturedStructure.GetName()}!", MessageType.Warning);
                            }

                            capturedStructure.OwnerId = unit.OwnerId;
                            game.CurrentPlayer.Structures.Add(capturedStructure);
                            game.CurrentPlayer.RecordStructureCapture();
                            AddMessage($"🏆 You captured {capturedStructure.GetName()}!", MessageType.Success);
                        }
                    }

                    unit.MovementPoints = 0;

                    SelectUnit(unit);
                    UpdateResourceDisplay(); // refresh per-turn income immediately if the advance captured a tile

                    string unitName = unit.GetName().ToLower();
                    string enemyName = enemyUnit.GetName().ToLower();
                    string defenderName = defender?.Name ?? "Enemy";

                    if (unit.IsVeteran)
                        unitName = "veteran " + unitName;
                    if (enemyUnit.IsVeteran)
                        enemyName = "veteran " + enemyName;

                    AddMessage($"⚔️ {defenderName}'s {enemyName} was defeated by your {unitName}!", MessageType.Combat);

                    // Check if defender was in a structure's storage
                    if (enemyUnit is AirUnit defeatedAirUnit && defeatedAirUnit.HomeBaseId != -1)
                    {
                        Structure? homeBase = defender?.Structures.FirstOrDefault(s => s.StructureId == defeatedAirUnit.HomeBaseId);
                        if (homeBase is Base baseStructure)
                        {
                            baseStructure.Airport.Remove(defeatedAirUnit);
                        }
                        else if (homeBase is City city)
                        {
                            city.Airport.Remove(defeatedAirUnit);
                        }
                    }
                }
                // Update vision and refresh map
                game.CurrentPlayer.UpdateVision(game.Map);
                RenderMap();
                return;
            }

            // Check if destination is occupied by friendly units (max 3 stackable units)
            List<Unit> friendlyUnitsAtDestination = destinationTile.Units
                .Where(u => u.OwnerId == game.CurrentPlayer.PlayerId && !(u is Satellite))
                .ToList();

            if (friendlyUnitsAtDestination.Count >= MAX_UNITS_PER_TILE)
            {
                AddMessage($"Cannot stack more than {MAX_UNITS_PER_TILE} units on one tile!", MessageType.Warning);
                return;
            }

            // Check movement cost
            movementCost = destinationTile.GetMovementCost(unit);
            if (unit.MovementPoints < movementCost)
            {
                AddMessage("Not enough movement points!");
                return;
            }

            // Calculate path and check if reachable
            List<TilePosition> path = game.Map.FindPath(unit.Position, destination, unit);
            if (path.Count == 0)
            {
                AddMessage("Cannot reach that location!");
                return;
            }

            // Calculate total path cost
            double totalCost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                Tile tile = game.Map.GetTile(path[i]);
                totalCost += tile.GetMovementCost(unit);
            }

            if (totalCost > unit.MovementPoints)
            {
                AddMessage("Not enough movement points for that path!");
                return;
            }

            // Special handling for aircraft landing
            if (unit is AirUnit landingAirUnit && landingAirUnit.HomeBaseId == -1)
            {
                // Check if landing on friendly base/city
                if (destinationTile.Structure != null &&
                    destinationTile.Structure.OwnerId == unit.OwnerId)
                {
                    if (destinationTile.Structure is Base baseStructure)
                    {
                        if (baseStructure.Airport.Count >= Base.MAX_AIRPORT_CAPACITY)
                        {
                            AddMessage("Airport is full!");
                            return;
                        }

                        // Land the aircraft
                        Tile startTile = game.Map.GetTile(unit.Position);
                        startTile.Units.Remove(unit);

                        baseStructure.Airport.Add(landingAirUnit);
                        landingAirUnit.HomeBaseId = baseStructure.StructureId;
                        landingAirUnit.Fuel = landingAirUnit.MaxFuel;
                        landingAirUnit.MovementPoints = 0;
                        landingAirUnit.Position = new TilePosition(-1, -1);

                        AddMessage($"{landingAirUnit.GetName()} landed at base.");
                        SelectStructure(baseStructure);
                        game.CurrentPlayer.UpdateVision(game.Map);
                        RenderMap();
                        return;
                    }
                    else if (destinationTile.Structure is City city)
                    {
                        if (city.Airport.Count >= City.MAX_AIRPORT_CAPACITY)
                        {
                            AddMessage("Airport is full!");
                            return;
                        }

                        Tile startTile = game.Map.GetTile(unit.Position);
                        startTile.Units.Remove(unit);

                        city.Airport.Add(landingAirUnit);
                        landingAirUnit.HomeBaseId = city.StructureId;
                        landingAirUnit.Fuel = landingAirUnit.MaxFuel;
                        landingAirUnit.MovementPoints = 0;
                        landingAirUnit.Position = new TilePosition(-1, -1);

                        AddMessage($"{landingAirUnit.GetName()} landed at city.");
                        SelectStructure(city);
                        game.CurrentPlayer.UpdateVision(game.Map);
                        RenderMap();
                        return;
                    }
                }
            }

            // Animate movement along path
            await AnimateUnitMovement(unit, path);

            unit.MovementPoints -= totalCost;
            if (unit.MovementPoints < 1.0)
                unit.MovementPoints = 0;

            // Claim tile ownership
            destinationTile.OwnerId = unit.OwnerId;

            // Check for structure capture on movement (no combat)
            if (destinationTile.Structure != null && destinationTile.Structure.OwnerId != unit.OwnerId)
            {
                Structure capturedStructure = destinationTile.Structure;
                Player? oldOwner = game.Players.FirstOrDefault(p => p.PlayerId == capturedStructure.OwnerId);
                if (oldOwner != null)
                {
                    oldOwner.RecordStructureLoss(capturedStructure);
                    oldOwner.Structures.Remove(capturedStructure);
                    AddMessage($"⚠️ {oldOwner.Name} lost {capturedStructure.GetName()}!", MessageType.Warning);
                }

                capturedStructure.OwnerId = unit.OwnerId;
                game.CurrentPlayer.Structures.Add(capturedStructure);
                game.CurrentPlayer.RecordStructureCapture();
                AddMessage($"🏰 You captured {capturedStructure.GetName()}!", MessageType.Success);
            }

            SelectUnit(unit);
            game.CurrentPlayer.UpdateVision(game.Map);
            game.UpdateSupplyLines(); // a captured mine/tile may re-link or cut supply lines
            UpdateResourceDisplay(); // refresh per-turn income immediately after capturing the tile
            RenderMap();
        }

        private async Task AnimateUnitMovement(Unit unit, List<TilePosition> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Tile oldTile = game.Map.GetTile(unit.Position);
                oldTile.Units.Remove(unit);

                unit.Position = path[i];
                Tile newTile = game.Map.GetTile(path[i]);
                newTile.Units.Add(unit);

                RenderMap();
                await Task.Delay(150); // 150ms between steps
            }
        }

        private void ShowUnitStackSelection(List<Unit> units, TilePosition position)
        {
            // Filter out satellites - they're untouchable and in orbit
            units = units.Where(u => !(u is Satellite)).ToList();

            // If no selectable units after filtering, return
            if (units.Count == 0)
                return;

            // Create a selection window
            Window selectionWindow = new Window
            {
                Title = $"Select Unit at ({position.X}, {position.Y})",
                Width = 350,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(10) };

            // Count only stackable units (satellites don't count)
            int stackableUnits = units.Count(u => !(u is Satellite));

            TextBlock label = new TextBlock
            {
                Text = $"{stackableUnits} units at this location (max 3):",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(label);

            // Show warning if at stack limit
            if (stackableUnits >= MAX_UNITS_PER_TILE)
            {
                TextBlock warningLabel = new TextBlock
                {
                    Text = "⚠ Stack limit reached!",
                    Foreground = System.Windows.Media.Brushes.Red,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(warningLabel);
            }

            ListBox listBox = new ListBox { Height = 180, Margin = new Thickness(0, 0, 0, 10) };

            foreach (Unit unit in units)
            {
                string unitInfo = $"{unit.GetName()} ({(unit.IsVeteran ? "Veteran" : "Regular")}) - " +
                                 $"Life: {unit.Life}/{unit.MaxLife}, Moves: {unit.MovementPoints:F1}/{unit.MaxMovementPoints}";

                if (unit is AirUnit airUnit)
                {
                    unitInfo += $", Fuel: {airUnit.Fuel}/{airUnit.MaxFuel}";
                }

                ListBoxItem item = new ListBoxItem
                {
                    Content = unitInfo,
                    Tag = unit
                };
                listBox.Items.Add(item);
            }

            // Select first item by default
            if (listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = 0;
            }

            stackPanel.Children.Add(listBox);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button selectButton = new Button
            {
                Content = "Select",
                Width = 80,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };

            selectButton.Click += (s, e) =>
            {
                if (listBox.SelectedItem is ListBoxItem item && item.Tag is Unit selectedUnit)
                {
                    selectionWindow.DialogResult = true;
                    selectionWindow.Close();
                    SelectUnit(selectedUnit);
                    CenterOnPosition(selectedUnit.Position);
                    RenderMap();
                }
            };

            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                IsCancel = true
            };

            cancelButton.Click += (s, e) =>
            {
                selectionWindow.DialogResult = false;
                selectionWindow.Close();
            };

            buttonPanel.Children.Add(selectButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            selectionWindow.Content = stackPanel;

            // Handle double-click on listbox for quick selection
            listBox.MouseDoubleClick += (s, e) =>
            {
                if (listBox.SelectedItem is ListBoxItem item && item.Tag is Unit selectedUnit)
                {
                    selectionWindow.DialogResult = true;
                    selectionWindow.Close();
                    SelectUnit(selectedUnit);
                    CenterOnPosition(selectedUnit.Position);
                    RenderMap();
                }
            };

            selectionWindow.ShowDialog();
        }


        // PART 4: UPDATED UpdateAirUnitDisplay METHOD - Replace your entire method with this:

        private void UpdateAirUnitDisplay(AirUnit airUnit)
        {
            FuelGaugePanel.Visibility = Visibility.Visible;

            // Update Fuel Bar
            double fuelPercent = ((double)airUnit.Fuel / airUnit.MaxFuel) * 100;
            FuelProgressBar.Value = fuelPercent;
            FuelProgressText.Text = $"{airUnit.Fuel}/{airUnit.MaxFuel}";

            // Color coding for fuel
            if (fuelPercent > 50)
                FuelProgressBar.Foreground = System.Windows.Media.Brushes.LimeGreen;
            else if (fuelPercent > 25)
                FuelProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
            else
                FuelProgressBar.Foreground = System.Windows.Media.Brushes.Red;

            // Calculate actual path distance to nearest base (in tiles)
            int distanceInTiles = airUnit.GetDistanceToNearestBase(game.Map, game.CurrentPlayer);

            if (distanceInTiles >= 0)
            {
                // Convert tile distance to turns needed
                // Each turn the aircraft moves MaxMovementPoints tiles and consumes 1 fuel
                int turnsNeeded = (int)Math.Ceiling((double)distanceInTiles / airUnit.MaxMovementPoints);

                FuelDistanceText.Text = $"Distance to nearest base: {distanceInTiles} tiles ({turnsNeeded} turn{(turnsNeeded != 1 ? "s" : "")})";
                FuelDistanceText.Foreground = System.Windows.Media.Brushes.LightBlue;

                // Show warning based on fuel vs turns needed
                if (airUnit.Fuel < turnsNeeded)
                {
                    int shortage = turnsNeeded - airUnit.Fuel;
                    FuelWarningText.Text = $"⚠ STRANDED! Need {shortage} more fuel to reach base";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.Red;
                    FuelWarningText.Visibility = Visibility.Visible;
                }
                else if (airUnit.Fuel <= turnsNeeded + 1)
                {
                    int margin = airUnit.Fuel - turnsNeeded;
                    FuelWarningText.Text = $"⚠ Low fuel margin! Only {margin} fuel to spare";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                    FuelWarningText.Visibility = Visibility.Visible;
                }
                else if (airUnit.Fuel <= turnsNeeded + 2)
                {
                    int margin = airUnit.Fuel - turnsNeeded;
                    FuelWarningText.Text = $"Fuel margin: {margin} turns (adequate)";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.Gold;
                    FuelWarningText.Visibility = Visibility.Visible;
                }
                else
                {
                    FuelWarningText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                FuelDistanceText.Text = "No friendly base available";
                FuelDistanceText.Foreground = System.Windows.Media.Brushes.Red;
                FuelWarningText.Visibility = Visibility.Collapsed;
            }
        }

        private void HandleGeosyncPlacement(TilePosition tilePos)
        {
            if (geosyncToPlace == null)
                return;

            // Check if the position is valid and visible
            if (!game.Map.IsValidPosition(tilePos))
            {
                AddMessage("Invalid deployment location!", MessageType.Warning);
                return;
            }

            Tile tile = game.Map.GetTile(tilePos);

            //// Check if player can see this location
            //if (!game.CurrentPlayer.FogOfWar.ContainsKey(tilePos) ||
            //    game.CurrentPlayer.FogOfWar[tilePos] == VisibilityLevel.Hidden)
            //{
            //    AddMessage("Cannot deploy satellite to unexplored territory!", MessageType.Warning);
            //    return;
            //}

            // Place the satellite
            geosyncToPlace.Position = tilePos;
            tile.Units.Add(geosyncToPlace);

            AddMessage($"🛰️ Geosynchronous Satellite deployed at ({tilePos.X}, {tilePos.Y})!", MessageType.Success);

            // Clear selection state
            isSelectingGeosyncLocation = false;
            geosyncToPlace = null;

            // Reset cursor
            MapCanvas.Cursor = Cursors.Arrow;

            // Update vision immediately
            game.CurrentPlayer.UpdateVision(game.Map);

            // Render the map to show the satellite
            RenderMap();
        }

        private void HandleBomberTargetSelection(TilePosition targetPos)
        {
            // Calculate path and validate range
            List<TilePosition> path = game.Map.FindPath(bomberForMission.Position, targetPos, bomberForMission);

            if (path.Count == 0)
            {
                MessageDialog.Warn(this, "No valid path to target!", "Bombing Run");
                return;
            }

            int totalDistance = path.Count * 2; // Round trip
            if (totalDistance > bomberForMission.MaxFuel)
            {
                MessageDialog.Warn(this, "Target out of range!", "Bombing Run");
                return;
            }

            // Set up the bombing run
            bomberForMission.TargetPosition = targetPos;
            bomberForMission.FlightPath = path;
            bomberForMission.CurrentOrders.Type = OrderType.BombingRun;

            MessageDialog.Show(this, $"Bombing mission set to ({targetPos.X}, {targetPos.Y}).", "Bombing Run", MessageDialog.IconSuccess);

            isSelectingBomberTarget = false;
            bomberForMission = null;
            MapCanvas.Cursor = Cursors.Arrow;
            BomberMissionPanel.Visibility = Visibility.Collapsed;
        }

        // Button Click Handlers

        private void PatrolButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit == null)
                return;

            // For aircraft, check if it's in an airport or on the map
            if (selectedUnit is AirUnit airUnit)
            {
                // Aircraft can be selected either from map or from airport list
                // If HomeBaseId is set, it's in a base
                Structure homeBase = null;

                if (airUnit.HomeBaseId != -1)
                {
                    // Find the base
                    foreach (Structure structure in game.CurrentPlayer.Structures)
                    {
                        if (structure.StructureId == airUnit.HomeBaseId)
                        {
                            homeBase = structure;
                            break;
                        }
                    }
                }
                else
                {
                    // Aircraft is on map, find nearest base
                    homeBase = airUnit.GetNearestBase(game.Map, game.CurrentPlayer);
                }

                if (homeBase == null)
                {
                    AddMessage("No friendly base available for patrol!", MessageType.Warning);
                    return;
                }

                patrolStartPosition = homeBase.Position;
                PatrolInstructionsText.Text = $"Patrol will start from {homeBase.GetName()} at ({homeBase.Position.X}, {homeBase.Position.Y}). Click on map to set waypoints (max 2).";
            }
            else
            {
                // For ground/sea units, start from current position
                patrolStartPosition = selectedUnit.Position;
                PatrolInstructionsText.Text = $"Starting from current position. Click on map to set waypoints (max 2).";
            }

            unitOnPatrol = selectedUnit;
            patrolWaypoints = new List<TilePosition>();
            isSelectingPatrolWaypoints = true;

            PatrolSetupPanel.Visibility = Visibility.Visible;
            PatrolWaypointsList.Items.Clear();
            PatrolWaypointsList.Items.Add($"Start: ({patrolStartPosition.X}, {patrolStartPosition.Y})");

            AddMessage("Click on map to set patrol waypoints", MessageType.Info);
        }

        private void SentryButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.SetSentry();
                MessageDialog.Show(this, $"{selectedUnit.GetName()} is on sentry. It will wake when enemies are spotted.", "Sentry");

                NextUnitButton_Click(sender, e);
            }
        }

        private void BuildUnitButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitTypesCombo.SelectedItem == null)
                return;

            ComboBoxItem? selectedItem = (ComboBoxItem)UnitTypesCombo.SelectedItem;

            if (!selectedItem.IsEnabled)
            {
                MessageDialog.Warn(this, "Cannot build this unit — either at capacity or insufficient resources!", "Build Unit");
                return;
            }

            // The tag already contains a properly constructed UnitProductionOrder
            UnitProductionOrder? order = (UnitProductionOrder)selectedItem.Tag;

            // Check capacity one more time before adding to queue
            bool canBuild = false;
            if (selectedStructure is Base baseStructure)
            {
                canBuild = baseStructure.CanBuildUnit(order.UnitType);
            }
            else if (selectedStructure is City city)
            {
                canBuild = city.CanBuildUnit(order.UnitType);
            }

            if (!canBuild)
            {
                MessageDialog.Warn(this, "Cannot build this unit — facility is at capacity!", "Build Unit");
                return;
            }

            // Check if player has sufficient resources (whatever the unit costs).
            if (order.Cost.Any(kv => game.CurrentPlayer.GetResource(kv.Key) < kv.Value))
            {
                MessageDialog.Warn(this, "Insufficient resources to build " + order.DisplayName + "!", "Build Unit");
                return;
            }

            // People-units are raised from the structure's populace (never dropping below 1).
            int popCost = UnitProductionOrder.PopulationCost(order.UnitType);
            if (popCost > 0 && selectedStructure.Population - popCost < 1)
            {
                MessageDialog.Warn(this, $"Not enough populace to build {order.DisplayName}!\n\nNeed {popCost + 1} (have {selectedStructure.Population:0.#}); populace can't drop below 1.", "Build Unit");
                return;
            }

            // Deduct the unit's full cost immediately when adding to queue (any resources).
            foreach (var kv in order.Cost)
                game.CurrentPlayer.AddResource(kv.Key, -kv.Value);

            if (popCost > 0)
                selectedStructure.Population -= popCost;

            // Add to production queue
            if (selectedStructure is Base baseStruct)
            {
                baseStruct.ProductionQueue.Enqueue(order);
                UpdateProductionQueue(baseStruct);
                UpdateStructureLists(baseStruct);
                PopulateAvailableUnits(baseStruct);
                UpdateResourceDisplay();
            }
            else if (selectedStructure is City cityStruct)
            {
                cityStruct.ProductionQueue.Enqueue(order);
                UpdateProductionQueue(cityStruct);
                UpdateStructureLists(cityStruct);
                PopulateAvailableUnits(cityStruct);
                UpdateResourceDisplay();
            }

            string paid = string.Join(" ", order.Cost.Where(kv => kv.Value > 0)
                .Select(kv => ResourceRegistry.Get(kv.Key).Symbol + kv.Value));
            AddMessage($"Resources paid! {paid}\n\n{order.DisplayName} added to production queue.");
        }

        private void LaunchMissionButton_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog.Show(this, "Click on the map to select a bomber target.", "Bombing Run");
        }

        private void CancelMissionButton_Click(object sender, RoutedEventArgs e)
        {
            isSelectingBomberTarget = false;
            bomberForMission = null;
            BomberMissionPanel.Visibility = Visibility.Collapsed;
        }

        private async void EndTurnButton_Click(object sender, RoutedEventArgs e)
        {
            if (game.HasSurrendered)
                return;

            // Check if any units still have movement points remaining
            List<Unit> unitsWithMovement = game.CurrentPlayer.Units
                .Where(u => u.MovementPoints >= 0.5 &&
                            !u.IsSkippedThisTurn &&
                            !u.IsAsleep &&
                            !(u is Satellite))
                .ToList();

            if (unitsWithMovement.Count > 0)
            {
                string unitWord = unitsWithMovement.Count == 1 ? "unit has" : "units have";
                bool answer = MessageDialog.Confirm(this,
                    $"{unitsWithMovement.Count} {unitWord} movement points remaining. End turn anyway?",
                    "Units Still Have Moves");

                if (!answer)
                    return;
            }

            currentUnitIndex = 0;
            currentStructureIndex = 0;

            game.NextTurn();

            // Update statistics for human player
            Player? humanPlayer = game.Players.FirstOrDefault(p => !p.IsAI);
            if (humanPlayer != null)
            {
                humanPlayer.UpdateStatistics(game.Map);
            }

            // Check for game over
            CheckForGameOver();

            // Display any production messages
            while (game.ProductionMessages.Count > 0)
            {
                string message = game.ProductionMessages.Dequeue();
                AddMessage(message, MessageType.Success);
            }

            AddMessage($"=== Turn {game.TurnNumber} begins ===", MessageType.Success);

            if (!game.CurrentPlayer.IsAI)
            {
                // Process automatic orders at the start of human turn
                await ProcessAutomaticOrdersWithVisuals();
            }

            // Process all AI turns WITHOUT rendering
            while (game.CurrentPlayer.IsAI)
            {
                AIThinkingPanel.Visibility = Visibility.Visible;
                AIStatusText.Text = $"{game.CurrentPlayer.Name} is planning...";
                NextUnitButton.Visibility = Visibility.Collapsed;
                UpdateEndTurnButtonImage();

                UpdateGameInfo();

                await Task.Delay(300);

                // Execute AI turn with message callback
                aiController.ExecuteAITurn(game.CurrentPlayer, (message, type) =>
                {
                    AddMessage(message, type);
                });

                game.NextTurn();

                // Display any production messages from AI turns too
                while (game.ProductionMessages.Count > 0)
                {
                    string message = game.ProductionMessages.Dequeue();
                    AddMessage(message, MessageType.Success);
                }

                // For AI players, auto-place any unplaced geosync satellites
                if (game.CurrentPlayer.IsAI)
                {
                    List<GeosynchronousSatellite> unplacedGeosync = game.CurrentPlayer.Units
                        .OfType<GeosynchronousSatellite>()
                        .Where(s => s.Position.X == -1 && s.Position.Y == -1)
                        .ToList();

                    foreach (GeosynchronousSatellite satellite in unplacedGeosync)
                    {
                        TilePosition randomPos = new TilePosition(
                            new Random().Next(0, game.Map.Width),
                            new Random().Next(0, game.Map.Height));

                        satellite.Position = randomPos;
                        Tile tile = game.Map.GetTile(randomPos);
                        tile.Units.Add(satellite);
                    }
                }
            }

            // Human is in control again — refresh the income readout immediately so any
            // resource tiles the AI captured from the player are reflected at once, before
            // automatic orders and combat replays are shown (not only at the very end).
            UpdateResourceDisplay();

            // AFTER all AI turns complete, process human automatic orders again
            if (!game.CurrentPlayer.IsAI && game.AutomaticOrdersQueue.Count > 0)
            {
                await ProcessAutomaticOrdersWithVisuals();
            }

            // Show combat replays for any AI attacks against the human player
            while (game.PendingCombatReplays.Count > 0)
            {
                CombatResult pendingCombat = game.PendingCombatReplays.Dequeue();
                RenderMap();
                CombatWindow combatWindow = new CombatWindow(pendingCombat);
                combatWindow.Owner = this;
                combatWindow.ShowDialog();
            }

            // Process completed builds and prompt for names
            ProcessCompletedBuilds();

            AIThinkingPanel.Visibility = Visibility.Collapsed;
            UpdateGameInfo();
            UpdateNextButton();
            UpdateEndTurnButtonImage();
            UpdateResourceDisplay();
            RenderMap();

            // Auto-select the first unit with movement so the player is oriented immediately
            currentUnitIndex = 0;
            NextUnitButton_Click(null, null);
        }
        private void RenderMapFromHumanPerspective()
        {
            Player humanPlayer = game.Players[0];
            WriteableBitmap bitmap = mapRenderer.RenderMap(humanPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            MapCanvas.Children.Clear();

            Image image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };

            Canvas.SetZIndex(image, 10);
            MapCanvas.Children.Add(image);

            // REMOVED: RenderResourceIcons(humanPlayer);
        }

        private void SaveGameButton_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog.Show(this, "Save not yet implemented.", "Save Game");
        }

        private void LoadGameButton_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog.Show(this, "Load not yet implemented.", "Load Game");
        }

        private void UpdateSubmarineDisplay(Submarine submarine)
        {
            SubmarinePanel.Visibility = Visibility.Visible;

            if (submarine.IsSubmerged)
            {
                SubmarineStatusText.Text = "🌊 Status: Submerged (Stealth Active)";
                SubmarineStatusText.Foreground = System.Windows.Media.Brushes.Cyan;
            }
            else
            {
                SubmarineStatusText.Text = "⚠ Status: Surfaced (Vulnerable)";
                SubmarineStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void UpdateCarrierDisplay(Carrier carrier)
        {
            CarrierCapacityPanel.Visibility = Visibility.Visible;
            CarrierCapacityLabel.Text = "Aircraft Docked:";

            double capacityPercent = ((double)carrier.DockedAircraft.Count / carrier.Capacity) * 100;
            CarrierCapacityBar.Value = capacityPercent;
            CarrierCapacityText.Text = $"{carrier.DockedAircraft.Count}/{carrier.Capacity}";

            // Color coding
            if (capacityPercent < 80)
                CarrierCapacityBar.Foreground = System.Windows.Media.Brushes.SkyBlue;
            else
                CarrierCapacityBar.Foreground = System.Windows.Media.Brushes.Orange;

            // List docked aircraft
            CarrierContentsList.Items.Clear();
            foreach (AirUnit aircraft in carrier.DockedAircraft)
            {
                CarrierContentsList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}/{aircraft.MaxFuel}");
            }
        }

        private void UpdateTransportDisplay(Transport transport)
        {
            CarrierCapacityPanel.Visibility = Visibility.Visible;
            CarrierCapacityLabel.Text = "Units Embarked:";

            double capacityPercent = ((double)transport.EmbarkedUnits.Count / transport.Capacity) * 100;
            CarrierCapacityBar.Value = capacityPercent;
            CarrierCapacityText.Text = $"{transport.EmbarkedUnits.Count}/{transport.Capacity}";

            // Color coding
            if (capacityPercent < 80)
                CarrierCapacityBar.Foreground = System.Windows.Media.Brushes.ForestGreen;
            else
                CarrierCapacityBar.Foreground = System.Windows.Media.Brushes.Orange;

            // List embarked units
            CarrierContentsList.Items.Clear();
            foreach (LandUnit embarkedUnit in transport.EmbarkedUnits)
            {
                CarrierContentsList.Items.Add($"{embarkedUnit.GetName()} - Life: {embarkedUnit.Life}/{embarkedUnit.MaxLife}");
            }
        }

        private void UpdatePatrolBoatDisplay(PatrolBoat patrolBoat)
        {
            // Check if in deep water
            Tile tile = game.Map.GetTile(patrolBoat.Position);
            bool isDeepWater = !tile.IsCoastalWater(game.Map) && tile.Terrain == TerrainType.Ocean;

            if (isDeepWater)
            {
                PatrolBoatWarningText.Text = "⚠ DEEP WATER PENALTY: -40% Toughness & Movement";
                PatrolBoatWarningText.Visibility = Visibility.Visible;
            }
        }

        private void UpdateArtilleryDisplay(Artillery artillery)
        {
            ArtilleryRangeText.Text = $"🎯 Attack Range: {artillery.AttackRange} tiles";
            ArtilleryRangeText.Visibility = Visibility.Visible;
        }

        private void UpdateAntiAircraftDisplay(AntiAircraft antiAircraft)
        {
            // Check proximity to friendly base
            bool nearBase = false;
            foreach (Structure structure in game.CurrentPlayer.Structures)
            {
                if (structure is Base || structure is City)
                {
                    int distance = Math.Abs(antiAircraft.Position.X - structure.Position.X) +
                                 Math.Abs(antiAircraft.Position.Y - structure.Position.Y);
                    if (distance <= 2)
                    {
                        nearBase = true;
                        break;
                    }
                }
            }

            if (nearBase)
            {
                AntiAircraftProximityText.Text = "✓ Within Range of Base (Can Attack Aircraft)";
                AntiAircraftProximityText.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                AntiAircraftProximityText.Text = "⚠ Too Far From Base (Cannot Attack)";
                AntiAircraftProximityText.Foreground = System.Windows.Media.Brushes.Red;
            }

            AntiAircraftProximityText.Visibility = Visibility.Visible;
        }

        private void UpdateSpyDisplay(Spy spy)
        {
            if (spy.IsRevealed)
            {
                SpyStatusText.Text = "⚠ REVEALED! Disguise blown";
                SpyStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                SpyStatusText.Text = "✓ Disguise Active (Appears as Army to enemies)";
                SpyStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }

            SpyStatusText.Visibility = Visibility.Visible;
        }

        // NEW BUTTON HANDLERS - Add these methods:

        private async void ReturnToBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is AirUnit airUnit)
            {
                // Cancel any patrol orders
                if (selectedUnit.CurrentOrders.Type == OrderType.Patrol)
                {
                    selectedUnit.CurrentOrders.Type = OrderType.None;
                    selectedUnit.CurrentOrders.PatrolWaypoints.Clear();
                    AddMessage($"{airUnit.GetName()} patrol cancelled", MessageType.Info);
                }

                Structure? nearestBase = airUnit.GetNearestBase(game.Map, game.CurrentPlayer);
                if (nearestBase != null)
                {
                    game.AddAutomaticOrder(airUnit, nearestBase.Position, AutomaticOrderType.ReturnToBase);

                    AddMessage($"{airUnit.GetName()} ordered to return to {nearestBase.GetName()} at ({nearestBase.Position.X}, {nearestBase.Position.Y}).", MessageType.Movement);

                    await ProcessAutomaticOrdersWithVisuals();

                    game.CurrentPlayer.UpdateVision(game.Map);
                    RenderMap();

                    Tile tile = game.Map.GetTile(airUnit.Position);
                    if (!tile.Units.Contains(airUnit))
                    {
                        ClearSelection();
                    }
                    else if (selectedUnit == airUnit)
                    {
                        SelectUnit(airUnit);
                    }

                    UpdateNextButton();
                }
                else
                {
                    AddMessage("No friendly base found!", MessageType.Warning);
                }
            }
        }

       private void ParkVehicleButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit == null)
                return;

            Tile currentTile = game.Map.GetTile(selectedUnit.Position);
            
            // Check if unit is on a friendly base/city
            if (currentTile.Structure != null && 
                currentTile.Structure.OwnerId == game.CurrentPlayer.PlayerId &&
                (currentTile.Structure is Base || currentTile.Structure is City))
            {
                Structure structure = currentTile.Structure;
                
                // Infantry units (Army, Sapper, Spy) go to barracks
                if (selectedUnit is Army || selectedUnit is Sapper || selectedUnit is Spy)
                {
                    if (structure is Base baseStructure)
                    {
                        if (baseStructure.Barracks.Count >= Base.MAX_BARRACKS_CAPACITY)
                        {
                            AddMessage("Barracks is full!", MessageType.Warning);
                            return;
                        }
                        
                        currentTile.Units.Remove(selectedUnit);
                        selectedUnit.Position = new TilePosition(-1, -1);
                        baseStructure.Barracks.Add(selectedUnit as LandUnit);
                        AddMessage($"{selectedUnit.GetName()} moved to barracks at {structure.GetName()}", MessageType.Success);
                    }
                    else if (structure is City city)
                    {
                        if (city.Barracks.Count >= City.MAX_BARRACKS_CAPACITY)
                        {
                            AddMessage("Barracks is full!", MessageType.Warning);
                            return;
                        }

                        currentTile.Units.Remove(selectedUnit);
                        selectedUnit.Position = new TilePosition(-1, -1);
                        city.Barracks.Add(selectedUnit as LandUnit);
                        AddMessage($"{selectedUnit.GetName()} moved to barracks at {structure.GetName()}", MessageType.Success);
                    }
                }
                // Vehicles (Tank, Artillery, AntiAircraft) go to motor pool
                else if (selectedUnit is Tank || selectedUnit is Artillery || selectedUnit is AntiAircraft)
                {
                    if (structure is Base baseStructure)
                    {
                        currentTile.Units.Remove(selectedUnit);
                        selectedUnit.Position = new TilePosition(-1, -1);
                        baseStructure.MotorPool.Add(selectedUnit);
                        AddMessage($"{selectedUnit.GetName()} parked in motor pool at {structure.GetName()}", MessageType.Success);
                    }
                    else if (structure is City city)
                    {
                        currentTile.Units.Remove(selectedUnit);
                        selectedUnit.Position = new TilePosition(-1, -1);
                        city.MotorPool.Add(selectedUnit);
                        AddMessage($"{selectedUnit.GetName()} parked in motor pool at {structure.GetName()}", MessageType.Success);
                    }
                }

                ClearSelection();
                SelectStructure(structure);
                game.CurrentPlayer.UpdateVision(game.Map);
                RenderMap();
            }
            else
            {
                AddMessage("Must be on a friendly base or city to park unit!", MessageType.Warning);
            }
        }

        private void LandAtBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is AirUnit airUnit)
            {
                List<Structure> adjacentStructures = new List<Structure>();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        TilePosition checkPos = new TilePosition(airUnit.Position.X + dx, airUnit.Position.Y + dy);
                        if (game.Map.IsValidPosition(checkPos))
                        {
                            Tile tile = game.Map.GetTile(checkPos);
                            if (tile.Structure != null &&
                                tile.Structure.OwnerId == game.CurrentPlayer.PlayerId &&
                                (tile.Structure is Base || tile.Structure is City))
                            {
                                adjacentStructures.Add(tile.Structure);
                            }
                        }
                    }
                }

                if (adjacentStructures.Count > 0)
                {
                    Structure structure = adjacentStructures[0];

                    Tile tile = game.Map.GetTile(airUnit.Position);
                    tile.Units.Remove(airUnit);

                    if (structure is Base baseStructure)
                    {
                        if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                        {
                            baseStructure.Airport.Add(airUnit);
                            airUnit.HomeBaseId = baseStructure.StructureId;
                            airUnit.Fuel = airUnit.MaxFuel;
                            AddMessage($"{airUnit.GetName()} landed and refueled at {structure.GetName()}", MessageType.Success);

                            ClearSelection();
                            SelectStructure(structure);
                            game.CurrentPlayer.UpdateVision(game.Map);
                            RenderMap();
                        }
                        else
                        {
                            tile.Units.Add(airUnit);
                            AddMessage("Airport is full!", MessageType.Warning);
                        }
                    }
                    else if (structure is City city)
                    {
                        if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                        {
                            city.Airport.Add(airUnit);
                            airUnit.HomeBaseId = city.StructureId;
                            airUnit.Fuel = airUnit.MaxFuel;
                            AddMessage($"{airUnit.GetName()} landed and refueled at {structure.GetName()}", MessageType.Success);

                            ClearSelection();
                            SelectStructure(structure);
                            game.CurrentPlayer.UpdateVision(game.Map);
                            RenderMap();
                        }
                        else
                        {
                            tile.Units.Add(airUnit);
                            AddMessage("Airport is full!", MessageType.Warning);
                        }
                    }
                }
                else
                {
                    AddMessage("Not adjacent to a friendly base!", MessageType.Warning);
                }
            }
        }
        private void ToggleSubmergeButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is Submarine submarine)
            {
                submarine.IsSubmerged = !submarine.IsSubmerged;
                UpdateSubmarineDisplay(submarine);
                MessageDialog.Show(this,
                    submarine.IsSubmerged ? "Submarine submerged." : "Submarine surfaced.",
                    "Submarine");
            }
        }

        private void SurrenderButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm surrender
            bool result = MessageDialog.Confirm(this,
                "Are you sure you want to surrender?\n\n" +
                "This will reveal the entire map showing all units and structures.\n" +
                "You will no longer be able to issue commands.",
                "Surrender?");

            if (result)
            {
                // Reveal the entire map
                game.RevealEntireMap();

                // Update the display
                RenderMap();

                // Disable game controls
                DisableGameControls();

                // Show surrender message
                MessageDialog.Show(this,
                    "You have surrendered.\n\n" +
                    "The entire map is now visible.\n" +
                    "Red units belong to your opponents.\n\n" +
                    "Press New Game to play again.",
                    "Surrendered");
            }
        }

        private void DisableGameControls()
        {
            // Disable unit commands
            if (CircularPatrolButton != null) CircularPatrolButton.IsEnabled = false;
            if (CircularSkipButton != null) CircularSkipButton.IsEnabled = false;
            if (CircularSleepButton != null) CircularSleepButton.IsEnabled = false;
            if (CircularSentryButton != null) CircularSentryButton.IsEnabled = false;
            if (CircularWakeUpButton != null) CircularWakeUpButton.IsEnabled = false;

            // Disable structure commands
            if (BuildUnitButton != null) BuildUnitButton.IsEnabled = false;
            if (TakeOffButton != null) TakeOffButton.IsEnabled = false;
            if (CircularLandButton != null) CircularLandButton.IsEnabled = false;
            if (CircularRTBButton != null) CircularRTBButton.IsEnabled = false;
            if (CircularBombButton != null) CircularBombButton.IsEnabled = false;
            if (RepairAircraftButton != null) RepairAircraftButton.IsEnabled = false;
            if (LaunchShipButton != null) LaunchShipButton.IsEnabled = false;
            if (RepairShipButton != null) RepairShipButton.IsEnabled = false;
            if (DeployMotorButton != null) DeployMotorButton.IsEnabled = false;
            if (DeployArmyButton != null) DeployArmyButton.IsEnabled = false;
            if (CircularSubmergeButton != null) CircularSubmergeButton.IsEnabled = false;

            // Disable game flow buttons
            EndTurnButton.IsEnabled = false;
            NextUnitButton.IsEnabled = false;
            SurrenderButton.IsEnabled = false;
            SurrenderButton.Opacity = 0.5;

            // Clear selection
            ClearSelection();

            // Show New Game button if it exists
            if (NewGameButton != null)
            {
                NewGameButton.Visibility = Visibility.Visible;
            }
        }

        private void EnableGameControls()
        {
            // Re-enable unit commands
            if (CircularPatrolButton != null) CircularPatrolButton.IsEnabled = true;
            if (CircularSkipButton != null) CircularSkipButton.IsEnabled = true;
            if (CircularSleepButton != null) CircularSleepButton.IsEnabled = true;
            if (CircularSentryButton != null) CircularSentryButton.IsEnabled = true;

            // Re-enable structure commands
            if (BuildUnitButton != null) BuildUnitButton.IsEnabled = true;

            // Re-enable game flow buttons
            EndTurnButton.IsEnabled = true;
            NextUnitButton.IsEnabled = true;
            SurrenderButton.IsEnabled = true;
            SurrenderButton.Opacity = 1.0;

            // Hide New Game button
            if (NewGameButton != null)
            {
                NewGameButton.Visibility = Visibility.Collapsed;
            }
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            LaunchNewGame();
        }

        private void ExitGameButton_Click(object sender, RoutedEventArgs e)
        {
            _suppressExitConfirmation = false; // allow the normal OnClosing confirm
            Close();
        }

        private void LaunchNewGame()
        {
            StartGameForm startForm = new StartGameForm();
            if (startForm.ShowDialog() == true)
            {
                gameSettings = startForm.Settings;
                InitializeGame();
                EnableGameControls();
            }
        }
        private void UpdateEndTurnButtonImage()
        {
            if (endTurnImage != null)
            {
                if (game.CurrentPlayer.IsAI)
                {
                    endTurnImage.Source = new BitmapImage(new Uri("/Resources/unavailable.png", UriKind.Relative));
                    EndTurnButton.IsEnabled = false;
                    EndTurnButton.Opacity = 0.5;
                }
                else
                {
                    endTurnImage.Source = new BitmapImage(new Uri("/Resources/fast-forward.png", UriKind.Relative));
                    EndTurnButton.IsEnabled = true;
                    EndTurnButton.Opacity = 1.0;
                }
            }
        }
        private void UpdateResourceDisplay()
        {
            Player player = game.CurrentPlayer;
            var income = player.GetResourceIncome(game.Map);

            // Rebuild one chip per registry currency, so adding a resource shows up automatically.
            ResourcePanel.Children.Clear();
            foreach (var rt in ResourceRegistry.Currencies)
            {
                var def = ResourceRegistry.Get(rt);
                var chip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0) };

                var icon = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                try { icon.Source = new BitmapImage(new Uri(def.IconPath, UriKind.Relative)); } catch { }
                chip.Children.Add(icon);

                chip.Children.Add(new TextBlock
                {
                    Text = player.GetResource(rt).ToString(),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(def.ColorHex),
                    Margin = new Thickness(0, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                int inc = income.TryGetValue(rt, out var v) ? v : 0;
                chip.Children.Add(new TextBlock
                {
                    Text = $"(+{inc})",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.LightGreen,
                    VerticalAlignment = VerticalAlignment.Center
                });

                ResourcePanel.Children.Add(chip);
            }
        }

        private async Task ProcessAutomaticOrdersWithVisuals()
        {
            if (game.AutomaticOrdersQueue.Count == 0)
                return;

            List<AutomaticOrder> ordersToProcess = game.AutomaticOrdersQueue.ToList();
            game.AutomaticOrdersQueue.Clear();

            List<Unit> enemiesSpottedUnits = new List<Unit>();

            foreach (AutomaticOrder order in ordersToProcess)
            {
                // Skip if unit is dead or belongs to a different player
                if (order.Unit.Life <= 0 || order.Unit.OwnerId != game.CurrentPlayer.PlayerId)
                    continue;

                // Process the order with visual updates
                (bool shouldContinue, bool enemySpotted) = await game.ProcessAutomaticOrder(
                    order,
                    (unit) =>
                    {
                        // Update vision callback
                        game.CurrentPlayer.UpdateVision(game.Map);
                    },
                    async () =>
                    {
                        // Render callback - update the display
                        RenderMapFromHumanPerspective();

                        // Allow UI to update
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                        // Now delay to show the movement (increased to 1 second)
                        await Task.Delay(gameSettings.AnimationDelay);
                    }
                );


                if (enemySpotted)
                {
                    // Enemy spotted - cancel automatic movement
                    enemiesSpottedUnits.Add(order.Unit);
                }
                else if (shouldContinue)
                {
                    // Re-enqueue if not complete and no enemies spotted
                    game.AutomaticOrdersQueue.Enqueue(order);
                }
            }

            // Final render after all movements
            RenderMapFromHumanPerspective();

            // Show alert if any units spotted enemies
            if (enemiesSpottedUnits.Count > 0)
            {
                string unitNames = string.Join(", ", enemiesSpottedUnits.Select(u => u.GetName()));
                MessageDialog.Warn(this,
                    $"⚠ ENEMY SPOTTED!\n\n{unitNames} detected enemy forces and halted automatic movement.\n\nThe unit(s) are now under your control.",
                    "Enemy Contact");

                // Select the first unit that spotted an enemy
                if (enemiesSpottedUnits.Count > 0)
                {
                    SelectUnit(enemiesSpottedUnits[0]);
                    CenterOnPosition(enemiesSpottedUnits[0].Position);
                }
            }
        }


        private void AddMessage(string message, MessageType type = MessageType.Info)
        {
            SolidColorBrush brush = type switch
            {
                MessageType.Success => System.Windows.Media.Brushes.LimeGreen,
                MessageType.Warning => System.Windows.Media.Brushes.Orange,
                MessageType.Error => System.Windows.Media.Brushes.Red,
                MessageType.Combat => System.Windows.Media.Brushes.Yellow,
                MessageType.Critical => System.Windows.Media.Brushes.Magenta,
                _ => System.Windows.Media.Brushes.White
            };

            messageLog.AddMessage(message, type);

            // Update the UI display
            UpdateMessageLog();
        }

        private void UpdateMessageLog()
        {
            MessageLogItems.ItemsSource = null;
            MessageLogItems.ItemsSource = messageLog.GetMessages();
            MessageCountText.Text = $"[{messageLog.GetMessages().Count}]";

            // Auto-scroll to bottom
            MessageScrollViewer.UpdateLayout();
            MessageScrollViewer.ScrollToEnd();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
            UpdateZoomDisplay();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
            UpdateZoomDisplay();
        }

        private void UpdateZoomDisplay()
        {
            ZoomLevelText.Text = $"{TILE_SIZE}x{TILE_SIZE}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                ZoomIn();
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                ZoomOut();
            }
        }

        private void ZoomIn()
        {
            if (TILE_SIZE < MAX_TILE_SIZE)
            {
                TILE_SIZE += 8;  // Increase by 8 pixels
                RecreateMapRenderer();
                RenderMap();
                AddMessage($"Zoom: {TILE_SIZE}x{TILE_SIZE}", MessageType.Info);
            }
        }

        private void ZoomOut()
        {
            if (TILE_SIZE > MIN_TILE_SIZE)
            {
                TILE_SIZE -= 8;  // Decrease by 8 pixels
                RecreateMapRenderer();
                RenderMap();
                AddMessage($"Zoom: {TILE_SIZE}x{TILE_SIZE}", MessageType.Info);
            }
        }

        private void RecreateMapRenderer()
        {
            mapRenderer = new MapRenderer(game, TILE_SIZE);
        }

        public class SatelliteProductionOrder : UnitProductionOrder
        {
            public OrbitType OrbitType { get; set; }

            public SatelliteProductionOrder(Type unitType, string displayName, OrbitType orbitType)
                : base(unitType, displayName)
            {
                OrbitType = orbitType;
            }
        }

        private void CheckForGameOver()
        {
            Player? humanPlayer = game.Players.FirstOrDefault(p => !p.IsAI);
            if (humanPlayer != null && game.IsPlayerEliminated(humanPlayer))
            {
                ShowGameOver(false);
            }
        }

        private void ShowGameOver(bool victory)
        {
            Player? humanPlayer = game.Players.FirstOrDefault(p => !p.IsAI);
            if (humanPlayer != null)
            {
                humanPlayer.Statistics.TurnsSurvived = game.TurnNumber;
                humanPlayer.Statistics.Victory = victory;

                GameOverWindow gameOverWindow = new GameOverWindow(humanPlayer.Statistics);
                gameOverWindow.Owner = this;
                gameOverWindow.ShowDialog();

                if (gameOverWindow.ReturnToMainMenu)
                {
                    LaunchNewGame();
                }
                else
                {
                    _suppressExitConfirmation = true;
                    Application.Current.Shutdown();
                }
            }
        }
        // Add this property near the top of your MainWindow class
        private double messageLogFontSize = 12.0;
        public double MessageLogFontSize
        {
            get { return messageLogFontSize; }
            set
            {
                messageLogFontSize = value;
                UpdateMessageLogFontSize();
            }
        }

        // Add these button click handlers
        private void IncreaseTextSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (messageLogFontSize < 20)
            {
                messageLogFontSize += 1;
                MessageTextSizeDisplay.Text = messageLogFontSize.ToString();
                UpdateMessageLogFontSize();
            }
        }

        private void DecreaseTextSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (messageLogFontSize > 8)
            {
                messageLogFontSize -= 1;
                MessageTextSizeDisplay.Text = messageLogFontSize.ToString();
                UpdateMessageLogFontSize();
            }
        }

        private void UpdateMessageLogFontSize()
        {
            if (MessageLogItems != null)
            {
                foreach (object? item in MessageLogItems.Items)
                {
                    ContentPresenter? container = MessageLogItems.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (container != null)
                    {
                        TextBlock? textBlock = FindVisualChild<TextBlock>(container);
                        if (textBlock != null)
                        {
                            textBlock.FontSize = messageLogFontSize;
                        }
                    }
                }

                // Force refresh
                MessageLogItems.Items.Refresh();
            }
        }

        // Helper method to find visual children
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                T? result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        private void BuildBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is Sapper sapper)
            {
                if (sapper.CanBuildBaseAt(sapper.Position, game.Map))
                {
                    sapper.StartBuildingBase(sapper.Position);
                    AddMessage($"Sapper began building a base. 2 turns remaining.", MessageType.Info);
                    SelectUnit(sapper);
                    RenderMap();
                }
                else
                {
                    AddMessage("Cannot build base here! Must be on suitable land with no existing structure.", MessageType.Error);
                }
            }
        }

        private void BuildBridgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is Sapper sapper)
            {
                // Let user click adjacent tile
                AddMessage("Click an adjacent water tile to build a bridge.", MessageType.Info);
                isSelectingBridgeTarget = true;
                sapperForBridge = sapper;
            }
        }

        private void CancelBuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is Sapper sapper)
            {
                sapper.ResetBuild();
                AddMessage("Build project cancelled.", MessageType.Info);
                SelectUnit(sapper);
                RenderMap();
            }
        }

        private void BuildMinerFromMineButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(selectedStructure is Mine mine) || mine.OwnerId != game.CurrentPlayer.PlayerId)
                return;

            int popCost = UnitProductionOrder.PopulationCost(typeof(Miner));
            var cost = UnitProductionOrder.GetCost(typeof(Miner));

            if (mine.Population - popCost < 1)
            {
                AddMessage($"Mine needs {popCost + 1} populace to build a Miner (has {mine.Population:0.#}).", MessageType.Warning);
                return;
            }
            if (cost.Any(kv => game.CurrentPlayer.GetResource(kv.Key) < kv.Value))
            {
                AddMessage("Not enough resources to build a Miner.", MessageType.Warning);
                return;
            }

            TilePosition pos = FindAdjacentEmptyTile(mine.Position);
            if (pos.X == -1)
            {
                AddMessage("No space next to the mine to deploy a Miner.", MessageType.Warning);
                return;
            }

            foreach (var kv in cost)
                game.CurrentPlayer.AddResource(kv.Key, -kv.Value);
            mine.Population -= popCost;

            var newMiner = new Miner { OwnerId = game.CurrentPlayer.PlayerId, Position = pos, MovementPoints = 0 };
            game.Map.GetTile(pos).Units.Add(newMiner);
            game.CurrentPlayer.Units.Add(newMiner);

            AddMessage("⛏️ The mine produced a Miner.", MessageType.Success);
            SelectStructure(mine);
            game.CurrentPlayer.UpdateVision(game.Map);
            UpdateResourceDisplay();
            RenderMap();
        }

        // ===== Civic upgrades (spend populace) =====
        private const int CostIndustry = CivicUpgrades.CostIndustry, CostFortify = CivicUpgrades.CostFortify,
                          CostWatchtower = CivicUpgrades.CostWatchtower, CostHousing = CivicUpgrades.CostHousing,
                          CostTreasury = CivicUpgrades.CostTreasury, CostMilitary1 = CivicUpgrades.CostMilitary1,
                          CostMilitary2 = CivicUpgrades.CostMilitary2, CostConscript = CivicUpgrades.CostConscript,
                          CostRepair = CivicUpgrades.CostRepair, SteelCostMilitary2 = CivicUpgrades.SteelCostMilitary2;

        private void RefreshCivicUpgrades(Structure s)
        {
            Player p = game.CurrentPlayer;
            bool own = s.OwnerId == p.PlayerId;
            double pop = s.Population;

            void Set(System.Windows.Controls.Button b, bool owned, string label, int cost, bool extraOk = true)
            {
                b.Content = owned ? $"{label} ✓" : $"{label} ({cost}👥)";
                b.IsEnabled = own && !owned && extraOk && (pop - cost >= 1);
            }

            Set(UpgIndustryButton, s.HasIndustry, "Industry", CostIndustry);
            Set(UpgFortifyButton, s.HasFortifications, "Fortify", CostFortify);
            Set(UpgWatchtowerButton, s.HasWatchtower, "Watchtower", CostWatchtower);
            Set(UpgHousingButton, s.HasHousing, "Housing", CostHousing);
            Set(UpgTreasuryButton, s.HasTreasury, "Treasury", CostTreasury);
            Set(UpgMilitary1Button, p.HasMilitary1, "Military I", CostMilitary1);

            UpgMilitary2Button.Content = p.HasMilitary2 ? "Military II ✓" : $"Military II ({CostMilitary2}👥+{SteelCostMilitary2}⚙️)";
            UpgMilitary2Button.IsEnabled = own && !p.HasMilitary2 && p.HasMilitary1 &&
                                           p.GetResource(ResourceType.Steel) >= SteelCostMilitary2 &&
                                           (pop - CostMilitary2 >= 1);

            UpgConscriptButton.Content = $"Conscript ({CostConscript}👥)";
            UpgConscriptButton.IsEnabled = own && (pop - CostConscript >= 1);

            UpgRepairButton.Content = s.Life < s.MaxLife ? $"Repair ({CostRepair}👥)" : "Repair ✓";
            UpgRepairButton.IsEnabled = own && s.Life < s.MaxLife && (pop - CostRepair >= 1);
        }

        private void AfterUpgrade()
        {
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            UpdateResourceDisplay();
            RenderMap();
        }

        private void UpgIndustry_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyIndustry(selectedStructure))
            { AddMessage("🏭 Industry built (+1.25% production).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgFortify_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyFortify(selectedStructure))
            { AddMessage("🧱 Fortifications built (+5 max life).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgWatchtower_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyWatchtower(selectedStructure))
            { AddMessage("🗼 Watchtower built (+1 vision).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgHousing_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyHousing(selectedStructure))
            { AddMessage("🏘️ Housing built (+0.5 populace/turn).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgTreasury_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyTreasury(selectedStructure))
            { AddMessage("🏦 Treasury built (+1 gold/turn).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgMilitary1_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyMilitary1(game.CurrentPlayer, selectedStructure))
            { AddMessage("🎖️ Military I researched (+1 Army health, all armies).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgMilitary2_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.BuyMilitary2(game.CurrentPlayer, selectedStructure))
            { AddMessage("🎖️ Military II researched (+1 Tank health, all tanks).", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgConscript_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.Conscript(game, game.CurrentPlayer, selectedStructure) != null)
            { AddMessage("🪖 Conscripted an Army.", MessageType.Success); AfterUpgrade(); }
        }

        private void UpgRepair_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStructure != null && CivicUpgrades.Repair(selectedStructure))
            { AddMessage("🔧 Structure fully repaired.", MessageType.Success); AfterUpgrade(); }
        }

        private void BuildMineButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(selectedUnit is Miner miner))
                return;

            Tile tile = game.Map.GetTile(miner.Position);
            if (!ResourceRegistry.IsMineable(tile.Resource))
            {
                AddMessage("A Miner must stand on a steel/oil tile to build a mine.", MessageType.Warning);
                return;
            }
            if (tile.Structure != null)
            {
                AddMessage("This tile already has a structure.", MessageType.Warning);
                return;
            }

            Mine mine = game.BuildMine(miner, game.CurrentPlayer);
            if (mine != null)
            {
                AddMessage($"⛏️ Built {mine.GetName()}" +
                           (mine.IsConnected ? " (supply line connected)." : " — no supply line to a base yet!"),
                           MessageType.Success);
                UpdateResourceDisplay();
                NextUnitButton_Click(sender, e);
            }
        }

        private void DisruptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(selectedUnit is Sapper sapper))
                return;

            sapper.IsDisruptingSupply = true;
            game.UpdateSupplyLines(); // auto-stops immediately if no enemy line crosses this tile
            if (sapper.IsDisruptingSupply)
            {
                sapper.MovementPoints = 0;
                AddMessage("🚫 Sapper disrupting supply line — immobile and defenseless until stopped.", MessageType.Info);
            }
            else
            {
                AddMessage("No enemy supply line crosses this tile to disrupt.", MessageType.Warning);
            }
            SelectUnit(sapper);
            UpdateResourceDisplay();
            RenderMap();
        }

        private void StopDisruptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(selectedUnit is Sapper sapper))
                return;

            sapper.IsDisruptingSupply = false;
            AddMessage("Sapper stopped disrupting supply lines.", MessageType.Info);
            game.UpdateSupplyLines();
            SelectUnit(sapper);
            UpdateResourceDisplay();
            RenderMap();
        }
        private void ProcessCompletedBuilds()
        {
            // Process completed bases
            while (game.CompletedBases.Count > 0)
            {
                (int playerId, Structure structure) = game.CompletedBases.Dequeue();

                if (playerId == game.Players[0].PlayerId) // Human player
                {
                    string name = PromptForBaseName(structure.Position);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        structure.CustomName = name;
                        AddMessage($"🏗️ New base '{name}' completed at ({structure.Position.X}, {structure.Position.Y})!", MessageType.Success);
                    }
                    else
                    {
                        AddMessage($"🏗️ New base completed at ({structure.Position.X}, {structure.Position.Y})!", MessageType.Success);
                    }
                }
                else
                {
                    // AI base - generate a simple name
                    structure.CustomName = $"{game.Players[playerId].Name}'s Base {game.Players[playerId].Structures.Count(s => s is Base)}";
                    AddMessage($"🏗️ {game.Players[playerId].Name} completed a new base!", MessageType.Info);
                }
            }

            // Process completed bridges
            while (game.CompletedBridges.Count > 0)
            {
                (int playerId, TilePosition position) = game.CompletedBridges.Dequeue();

                if (playerId == game.Players[0].PlayerId) // Human player
                {
                    string name = PromptForBridgeName(position);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        Tile tile = game.Map.GetTile(position);
                        tile.BridgeName = name;
                        AddMessage($"🌉 Bridge '{name}' completed at ({position.X}, {position.Y})!", MessageType.Success);
                    }
                    else
                    {
                        AddMessage($"🌉 Bridge completed at ({position.X}, {position.Y})!", MessageType.Success);
                    }
                }
                else
                {
                    AddMessage($"🌉 {game.Players[playerId].Name} completed a bridge!", MessageType.Info);
                }
            }
        }

        private string PromptForBaseName(TilePosition position)
        {
            Window dialog = new Window
            {
                Title = "Name Your Base",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 46))
            };

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(20) };

            TextBlock label = new TextBlock
            {
                Text = $"Your Sapper has completed a new base at ({position.X}, {position.Y})!",
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(label);

            TextBlock nameLabel = new TextBlock
            {
                Text = "Enter a name for this base:",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(nameLabel);

            TextBox textBox = new TextBox
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15),
                MaxLength = 30
            };
            stackPanel.Children.Add(textBox);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };

            Button skipButton = new Button
            {
                Content = "Skip",
                Width = 80
            };
            skipButton.Click += (s, e) =>
            {
                textBox.Text = "";
                dialog.DialogResult = true;
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(skipButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            textBox.Focus();
            dialog.ShowDialog();

            return textBox.Text.Trim();
        }

        private string PromptForBridgeName(TilePosition position)
        {
            Window dialog = new Window
            {
                Title = "Name Your Bridge",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 46))
            };

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(20) };

            TextBlock label = new TextBlock
            {
                Text = $"Your Sapper has completed a bridge at ({position.X}, {position.Y})!",
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(label);

            TextBlock nameLabel = new TextBlock
            {
                Text = "Enter a name for this bridge:",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(nameLabel);

            TextBox textBox = new TextBox
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 15),
                MaxLength = 30
            };
            stackPanel.Children.Add(textBox);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };

            Button skipButton = new Button
            {
                Content = "Skip",
                Width = 80
            };
            skipButton.Click += (s, e) =>
            {
                textBox.Text = "";
                dialog.DialogResult = true;
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(skipButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            textBox.Focus();
            dialog.ShowDialog();

            return textBox.Text.Trim();
        }
    }

}