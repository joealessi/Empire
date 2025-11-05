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

        private const int TILE_SIZE = 16;

        private int currentUnitIndex = 0;
        private int currentStructureIndex = 0;

        private const int MAX_UNITS_PER_TILE = 3;

        private Image endTurnImage;


        public MainWindow()
        {
            InitializeComponent();
            InitializeGame();
        }

        private void InitializeGame()
        {
            // Create a new game with 100x100 map and 2 players
            game = new Game(100, 100, 2);

            // Generate the map with proper starting positions
            GenerateMap();

            // Initialize the map renderer
            mapRenderer = new MapRenderer(game, TILE_SIZE);

            // Initialize AI controller
            aiController = new AIController(game);

            // Update vision for all players
            foreach (var player in game.Players)
            {
                player.UpdateVision(game.Map);
            }

            // Cache the image control reference
            EndTurnButton.ApplyTemplate();
            endTurnImage = EndTurnButton.Template.FindName("EndTurnImage", EndTurnButton) as Image;


            // Initial render
            RenderMap();
            UpdateGameInfo();
            UpdateNextButton();
            UpdateEndTurnButtonImage();
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
            var unitsWithMovement = game.CurrentPlayer.Units
                .Where(u => u.MovementPoints >= 0.5 &&
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
            var unitsWithMovement = game.CurrentPlayer.Units
                .Where(u => u.MovementPoints > 0 &&
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

                var unit = unitsWithMovement[currentUnitIndex];
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

                var structure = game.CurrentPlayer.Structures[currentStructureIndex];
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
                MessageBox.Show($"{selectedUnit.GetName()} will be skipped for this turn");

                // Update next button count
                UpdateNextButton();

                // Clear selection and move to next unit
                ClearSelection();
                RenderMap();
            }
        }

        private void SleepButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.Sleep();
                MessageBox.Show($"{selectedUnit.GetName()} is now asleep. Select it to wake it up.");

                // Update next button count
                UpdateNextButton();

                // Clear selection
                ClearSelection();
                RenderMap();
            }
        }

        private void WakeUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null && selectedUnit.IsAsleep)
            {
                selectedUnit.WakeUp();
                MessageBox.Show($"{selectedUnit.GetName()} is now awake!");

                // Update selection to refresh button states
                SelectUnit(selectedUnit);
                UpdateNextButton();
                RenderMap();
            }
        }


        private void CenterOnPosition(TilePosition pos)
        {
            // For now, just render the map
            // In a full implementation, you'd scroll the canvas to center on this position
            RenderMap();

            // TODO: Add scrolling to center the view on the unit/structure
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
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);
                    tile.Terrain = TerrainType.Ocean;
                }
            }

            // Step 2: Generate several large continents
            int numberOfContinents = game.Players.Count + rand.Next(1, 3); // At least one continent per player, plus extras
            List<TilePosition> continentCenters = new List<TilePosition>();

            for (int i = 0; i < numberOfContinents; i++)
            {
                // Space continents apart
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

            // Step 3: Smooth the map to remove single-tile anomalies
            SmoothMap();

            // Step 4: Add terrain variety to land
            AddTerrainVariety(rand);

            // Step 5: Mark coastal water
            MarkCoastalWater();

            // Step 6: Place player starting positions on large continents
            PlacePlayerStartingPositions(continentCenters, rand);
        }

        private bool IsTooCloseToOtherContinents(TilePosition pos, List<TilePosition> centers, int minDistance)
        {
            foreach (var center in centers)
            {
                int distance = Math.Abs(pos.X - center.X) + Math.Abs(pos.Y - center.Y);
                if (distance < minDistance)
                    return true;
            }
            return false;
        }

        private void GenerateContinent(TilePosition center, int size, Random rand)
        {
            // Use a growth algorithm to create organic-looking continents
            Queue<TilePosition> frontier = new Queue<TilePosition>();
            HashSet<TilePosition> visited = new HashSet<TilePosition>();

            frontier.Enqueue(center);
            visited.Add(center);

            int tilesPlaced = 0;

            while (frontier.Count > 0 && tilesPlaced < size)
            {
                var current = frontier.Dequeue();
                var tile = game.Map.GetTile(current);

                if (tile != null)
                {
                    tile.Terrain = TerrainType.Land;
                    tilesPlaced++;

                    // Add neighbors with probability that decreases with distance from center
                    int distanceFromCenter = Math.Abs(current.X - center.X) + Math.Abs(current.Y - center.Y);
                    double probability = 1.0 - (distanceFromCenter / (size / 10.0));
                    probability = Math.Max(0.3, Math.Min(0.95, probability));

                    // Check all four neighbors
                    int[] dx = { -1, 0, 1, 0 };
                    int[] dy = { 0, 1, 0, -1 };

                    for (int i = 0; i < 4; i++)
                    {
                        var neighborPos = new TilePosition(current.X + dx[i], current.Y + dy[i]);

                        if (game.Map.IsValidPosition(neighborPos) &&
                            !visited.Contains(neighborPos) &&
                            rand.NextDouble() < probability)
                        {
                            visited.Add(neighborPos);
                            frontier.Enqueue(neighborPos);
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
                        var pos = new TilePosition(x, y);
                        var tile = game.Map.GetTile(pos);

                        // Count land neighbors
                        int landNeighbors = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                var neighborPos = new TilePosition(x + dx, y + dy);
                                var neighbor = game.Map.GetTile(neighborPos);

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
                            var pos = new TilePosition(x, y);
                            var tile = game.Map.GetTile(pos);

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
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);

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
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);

                    if (tile.Terrain == TerrainType.Ocean)
                    {
                        // Check if adjacent to land
                        bool adjacentToLand = false;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                var neighborPos = new TilePosition(x + dx, y + dy);
                                if (game.Map.IsValidPosition(neighborPos))
                                {
                                    var neighbor = game.Map.GetTile(neighborPos);
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

            foreach (var center in continentCenters)
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

                // Create starting base
                var baseStructure = (Base)game.CreateStructure(typeof(Base), startPos, i);

                // Check if near coast for naval production and shipyard
                bool hasWater = HasAdjacentWater(startPos);
                baseStructure.CanProduceNaval = hasWater;
                baseStructure.HasShipyard = hasWater;

                game.Players[i].Structures.Add(baseStructure);
                game.Map.GetTile(startPos).Structure = baseStructure;
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
                var checkPos = new TilePosition(pos.X + dx[i], pos.Y + dy[i]);
                if (game.Map.IsValidPosition(checkPos))
                {
                    var tile = game.Map.GetTile(checkPos);
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
                var current = queue.Dequeue();
                var tile = game.Map.GetTile(current);

                if (tile != null && tile.Terrain != TerrainType.Ocean &&
                    tile.Terrain != TerrainType.CoastalWater)
                {
                    size++;

                    // Add neighbors
                    int[] dx = { -1, 0, 1, 0 };
                    int[] dy = { 0, 1, 0, -1 };

                    for (int i = 0; i < 4; i++)
                    {
                        var neighborPos = new TilePosition(current.X + dx[i], current.Y + dy[i]);
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
                        var pos = new TilePosition(x, y);

                        if (game.Map.IsValidPosition(pos))
                        {
                            var tile = game.Map.GetTile(pos);
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
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);

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
                    var checkPos = new TilePosition(pos.X + dx, pos.Y + dy);
                    if (game.Map.IsValidPosition(checkPos))
                    {
                        var tile = game.Map.GetTile(checkPos);
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

                    var pos = new TilePosition(basePos.X + dx, basePos.Y + dy);
                    if (game.Map.IsValidPosition(pos))
                    {
                        var tile = game.Map.GetTile(pos);
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
                    var temp = positions[i];
                    positions[i] = positions[j];
                    positions[j] = temp;
                }

                // Place 2 armies
                for (int i = 0; i < 2 && i < positions.Count; i++)
                {
                    var army = new Army { Position = positions[i], OwnerId = playerId };
                    game.Players[playerId].Units.Add(army);
                    game.Map.GetTile(positions[i]).Units.Add(army);
                }

                // Place 1 tank
                if (positions.Count > 2)
                {
                    var tank = new Tank { Position = positions[2], OwnerId = playerId };
                    game.Players[playerId].Units.Add(tank);
                    game.Map.GetTile(positions[2]).Units.Add(tank);
                }
            }
        }

        private void SetupTestScenario()
        {
            // Create a base for player 0
            var base1 = (Base)game.CreateStructure(typeof(Base), new TilePosition(10, 10), 0);
            base1.CanProduceNaval = false;
            game.Players[0].Structures.Add(base1);
            game.Map.GetTile(base1.Position).Structure = base1;

            // Create a base for player 1 (AI)
            var base2 = (Base)game.CreateStructure(typeof(Base), new TilePosition(90, 90), 1);
            base2.CanProduceNaval = false;
            game.Players[1].Structures.Add(base2);
            game.Map.GetTile(base2.Position).Structure = base2;

            // Create some starting units for player 0
            var army1 = new Army { UnitId = 1, Position = new TilePosition(11, 10), OwnerId = 0 };
            game.Players[0].Units.Add(army1);
            game.Map.GetTile(army1.Position).Units.Add(army1);

            var tank1 = new Tank { UnitId = 2, Position = new TilePosition(12, 10), OwnerId = 0 };
            game.Players[0].Units.Add(tank1);
            game.Map.GetTile(tank1.Position).Units.Add(tank1);

            // Update initial vision
            game.Players[0].UpdateVision(game.Map);
            game.Players[1].UpdateVision(game.Map);
        }

        private void RenderMap()
        {
            var bitmap = mapRenderer.RenderMap(game.CurrentPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };

            MapCanvas.Children.Clear();
            MapCanvas.Children.Add(image);
        }

        private void UpdateGameInfo()
        {
            TurnNumberText.Text = $"Turn: {game.TurnNumber}";
            CurrentPlayerText.Text = $"Player: {game.CurrentPlayer.Name}";
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (game.HasSurrendered)
                return;
            
            var clickPos = e.GetPosition(MapCanvas);
            var tilePos = new TilePosition((int)(clickPos.X / TILE_SIZE), (int)(clickPos.Y / TILE_SIZE));

            if (!game.Map.IsValidPosition(tilePos))
                return;

            if (isSelectingBomberTarget)
            {
                HandleBomberTargetSelection(tilePos);
                return;
            }

            var tile = game.Map.GetTile(tilePos);

            // Check for units at this position
            var friendlyUnits = tile.Units.Where(u => u.OwnerId == game.CurrentPlayer.PlayerId).ToList();

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

            // Clear selection
            ClearSelection();
            RenderMap();
        }


        private void MapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (game.HasSurrendered)
                return;

            if (selectedUnit == null)
                return;

            var clickPos = e.GetPosition(MapCanvas);
            var tilePos = new TilePosition((int)(clickPos.X / TILE_SIZE), (int)(clickPos.Y / TILE_SIZE));

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

            UnitInfoPanel.Visibility = Visibility.Visible;
            StructureInfoPanel.Visibility = Visibility.Collapsed;

            UnitNameText.Text = $"{unit.GetName()} ({(unit.IsVeteran ? "Veteran" : "Regular")})";
            UnitStatsText.Text = $"Power: {unit.Power}/{unit.MaxPower} | Toughness: {unit.Toughness}/{unit.MaxToughness}";

            // Update Life Bar
            double lifePercent = ((double)unit.Life / unit.MaxLife) * 100;
            LifeProgressBar.Value = lifePercent;
            LifeProgressText.Text = $"{unit.Life}/{unit.MaxLife}";

            // Color coding for life
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

            // Color coding for movement
            if (movementPercent > 50)
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.DodgerBlue;
            else if (movementPercent > 0)
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
            else
                MovementProgressBar.Foreground = System.Windows.Media.Brushes.DarkGray;

            // Hide all unit-specific panels by default
            FuelGaugePanel.Visibility = Visibility.Collapsed;
            SubmarinePanel.Visibility = Visibility.Collapsed;
            CarrierCapacityPanel.Visibility = Visibility.Collapsed;
            PatrolBoatWarningText.Visibility = Visibility.Collapsed;
            ArtilleryRangeText.Visibility = Visibility.Collapsed;
            AntiAircraftProximityText.Visibility = Visibility.Collapsed;
            SpyStatusText.Visibility = Visibility.Collapsed;

            // Handle unit-specific displays
            if (unit is AirUnit airUnit)
            {
                UpdateAirUnitDisplay(airUnit);
            }
            else if (unit is Submarine submarine)
            {
                UpdateSubmarineDisplay(submarine);
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

            // Show/Hide state buttons based on unit state
            if (selectedUnit.IsAsleep)
            {
                SkipTurnButton.Visibility = Visibility.Collapsed;
                SleepButton.Visibility = Visibility.Collapsed;
                SentryButton.Visibility = Visibility.Collapsed;
                WakeUpButton.Visibility = Visibility.Visible;
            }
            else
            {
                SkipTurnButton.Visibility = Visibility.Visible;
                SleepButton.Visibility = Visibility.Visible;
                SentryButton.Visibility = Visibility.Visible;
                WakeUpButton.Visibility = Visibility.Collapsed;
            }

            UpdateNextButton();
        }

        private void SelectStructure(Structure structure)
        {
            selectedStructure = structure;
            selectedUnit = null;

            UnitInfoPanel.Visibility = Visibility.Collapsed;
            StructureInfoPanel.Visibility = Visibility.Visible;

            StructureNameText.Text = structure.GetName();
            StructureLifeText.Text = $"Life: {structure.Life}/{structure.MaxLife}";

            if (structure is Base baseStructure)
            {
                DefenseBonusText.Text = $"Defense Bonus: +{baseStructure.GetDefenseBonus()}";
                UpdateStructureLists(baseStructure);
                UpdateProductionQueue(baseStructure);
                PopulateAvailableUnits(baseStructure);

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
                DefenseBonusText.Text = $"Defense Bonus: +{city.GetDefenseBonus()}";
                UpdateStructureLists(city);
                UpdateProductionQueue(city);
                PopulateAvailableUnits(city);

                // Cities don't have shipyards
                ShipyardLabel.Visibility = Visibility.Collapsed;
                ShipyardCapacityText.Visibility = Visibility.Collapsed;
                ShipyardList.Visibility = Visibility.Collapsed;
                LaunchShipButton.Visibility = Visibility.Collapsed;
                RepairShipButton.Visibility = Visibility.Collapsed;
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
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.DarkBlue;
            }

            foreach (var aircraft in baseStructure.Airport)
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
                    ShipyardCapacityText.Foreground = System.Windows.Media.Brushes.DarkBlue;
                }

                foreach (var ship in baseStructure.Shipyard)
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
            foreach (var unit in baseStructure.MotorPool)
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
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.DarkBlue;
            }

            foreach (var army in baseStructure.Barracks)
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
                AirportCapacityText.Foreground = System.Windows.Media.Brushes.DarkBlue;
            }

            foreach (var aircraft in city.Airport)
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
            foreach (var unit in city.MotorPool)
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
                BarracksCapacityText.Foreground = System.Windows.Media.Brushes.DarkBlue;
            }

            foreach (var army in city.Barracks)
            {
                BarracksList.Items.Add($"{army.GetName()} - Life: {army.Life}/{army.MaxLife}");
            }
        }


        private void AirportList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var aircraft = GetSelectedAircraft();
            TakeOffButton.IsEnabled = aircraft != null && !IsBeingRepaired(aircraft);
            BombingRunButton.IsEnabled = aircraft is Bomber && !IsBeingRepaired(aircraft);
            RepairAircraftButton.IsEnabled = aircraft != null && aircraft.Life < aircraft.MaxLife && !IsBeingRepaired(aircraft);
        }

        private void ShipyardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ship = GetSelectedShip();
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
            var aircraft = GetSelectedAircraft();
            if (aircraft == null || selectedStructure == null)
                return;

            int damageToRepair = aircraft.MaxLife - aircraft.Life;

            if (damageToRepair == 0)
            {
                MessageBox.Show("Unit is already at full health!");
                return;
            }

            // Start repair (1 turn per point of damage)
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.UnitsBeingRepaired[aircraft] = damageToRepair;
            }
            else if (selectedStructure is City city)
            {
                city.UnitsBeingRepaired[aircraft] = damageToRepair;
            }

            MessageBox.Show($"Repair started. Will take {damageToRepair} turn(s).");
            SelectStructure(selectedStructure);
        }

        private void RepairShipButton_Click(object sender, RoutedEventArgs e)
        {
            var ship = GetSelectedShip();
            if (ship == null || selectedStructure == null)
                return;

            int damageToRepair = ship.MaxLife - ship.Life;

            if (damageToRepair == 0)
            {
                MessageBox.Show("Unit is already at full health!");
                return;
            }

            // Start repair (1 turn per point of damage)
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.UnitsBeingRepaired[ship] = damageToRepair;
            }

            MessageBox.Show($"Repair started. Will take {damageToRepair} turn(s).");
            SelectStructure(selectedStructure);
        }

        private void LaunchShipButton_Click(object sender, RoutedEventArgs e)
        {
            var ship = GetSelectedShip();
            if (ship == null || selectedStructure == null)
                return;

            // Find an adjacent water tile
            var adjacentPos = FindAdjacentWaterTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageBox.Show("No water to launch into!");
                return;
            }

            // Remove from shipyard
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Shipyard.Remove(ship);
            }

            // Place on map
            ship.Position = adjacentPos;

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(ship);

            // Update display
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
                var pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

                if (game.Map.IsValidPosition(pos))
                {
                    var tile = game.Map.GetTile(pos);

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

        private Army GetSelectedArmyUnit()
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
            var aircraft = GetSelectedAircraft();
            if (aircraft == null || selectedStructure == null)
                return;

            // Find an adjacent empty tile
            var adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageBox.Show("No airspace to take off into!");
                return;
            }

            // Remove from airport
            if (selectedStructure is Base baseStructure)
            {
                baseStructure.Airport.Remove(aircraft);
            }
            else if (selectedStructure is City city)
            {
                city.Airport.Remove(aircraft);
            }

            // Place on map
            aircraft.Position = adjacentPos;
            aircraft.HomeBaseId = -1; // In the air
            aircraft.Fuel = aircraft.MaxFuel; // Refuel on takeoff

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(aircraft);

            // Update display
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void DeployMotorButton_Click(object sender, RoutedEventArgs e)
        {
            var unit = GetSelectedMotorUnit();
            if (unit == null || selectedStructure == null)
                return;

            // Find an adjacent empty tile
            var adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageBox.Show("No space to deploy unit!");
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

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(unit);

            // Update display
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void DeployArmyButton_Click(object sender, RoutedEventArgs e)
        {
            var unit = GetSelectedArmyUnit();
            if (unit == null || selectedStructure == null)
                return;

            // Find an adjacent empty tile
            var adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageBox.Show("No space to deploy unit!");
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

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(unit);

            // Update display
            SelectStructure(selectedStructure);
            game.CurrentPlayer.UpdateVision(game.Map);
            RenderMap();
        }

        private void BombingRunButton_Click(object sender, RoutedEventArgs e)
        {
            var bomber = GetSelectedAircraft() as Bomber;
            if (bomber == null)
                return;

            // First deploy the bomber to an adjacent tile
            var adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

            if (adjacentPos.X == -1)
            {
                MessageBox.Show("No airspace to take off into!");
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

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(bomber);

            // Now set up bombing run UI
            bomberForMission = bomber;
            isSelectingBomberTarget = true;

            BomberMissionPanel.Visibility = Visibility.Visible;
            BomberRangeText.Text = $"Bomber Range: {bomber.MaxFuel / 2} tiles (round trip)";

            // Populate available escorts from the airport
            AvailableEscortsList.Items.Clear();
            if (selectedStructure is Base baseStruct)
            {
                foreach (var aircraft in baseStruct.Airport)
                {
                    if (aircraft is Fighter)
                    {
                        AvailableEscortsList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}");
                    }
                }

                // Check for tankers
                var tankers = baseStruct.Airport.Where(a => a is Tanker).ToList();
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
                foreach (var aircraft in cityStruct.Airport)
                {
                    if (aircraft is Fighter)
                    {
                        AvailableEscortsList.Items.Add($"{aircraft.GetName()} - Fuel: {aircraft.Fuel}");
                    }
                }

                var tankers = cityStruct.Airport.Where(a => a is Tanker).ToList();
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

            MessageBox.Show("Select target on map for bombing run");

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
                var pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

                if (game.Map.IsValidPosition(pos))
                {
                    var tile = game.Map.GetTile(pos);

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
            foreach (var order in baseStructure.ProductionQueue)
            {
                ProductionQueueList.Items.Add($"{order.DisplayName} ({baseStructure.CurrentProductionProgress}/{order.TotalCost})");
            }
        }

        private void UpdateProductionQueue(City city)
        {
            ProductionQueueList.Items.Clear();
            foreach (var order in city.ProductionQueue)
            {
                ProductionQueueList.Items.Add($"{order.DisplayName} ({city.CurrentProductionProgress}/{order.TotalCost})");
            }
        }

        private void PopulateAvailableUnits(Structure structure)
        {
            UnitTypesCombo.Items.Clear();

            var baseStructure = structure as Base;
            var city = structure as City;

            // Helper to add unit if capacity allows
            void AddUnitIfCapacityAllows(string name, Type type, int cost)
            {
                bool canBuild = false;
                string capacityNote = "";

                if (baseStructure != null)
                {
                    canBuild = baseStructure.CanBuildUnit(type);

                    if (!canBuild)
                    {
                        if (type == typeof(Fighter) || type == typeof(Bomber) || type == typeof(Tanker))
                        {
                            capacityNote = " [Airport Full]";
                        }
                        else if (type == typeof(Carrier) || type == typeof(Battleship) ||
                                 type == typeof(Destroyer) || type == typeof(Submarine) ||
                                 type == typeof(PatrolBoat) || type == typeof(Transport))
                        {
                            capacityNote = " [Shipyard Full]";
                        }
                        else if (type == typeof(Army))
                        {
                            capacityNote = " [Barracks Full]";
                        }
                    }
                }
                else if (city != null)
                {
                    canBuild = city.CanBuildUnit(type);

                    if (!canBuild)
                    {
                        if (type == typeof(Fighter) || type == typeof(Bomber) || type == typeof(Tanker))
                        {
                            capacityNote = " [Airport Full]";
                        }
                        else if (type == typeof(Army))
                        {
                            capacityNote = " [Barracks Full]";
                        }
                    }
                }

                var item = new ComboBoxItem
                {
                    Content = $"{name} ({cost}){capacityNote}",
                    Tag = new { Type = type, Cost = cost },
                    IsEnabled = canBuild
                };
                UnitTypesCombo.Items.Add(item);
            }

            // Add available unit types
            AddUnitIfCapacityAllows("Army", typeof(Army), 30);
            AddUnitIfCapacityAllows("Tank", typeof(Tank), 60);
            AddUnitIfCapacityAllows("Artillery", typeof(Artillery), 50);
            AddUnitIfCapacityAllows("Anti-Aircraft", typeof(AntiAircraft), 40);
            AddUnitIfCapacityAllows("Fighter", typeof(Fighter), 70);
            AddUnitIfCapacityAllows("Bomber", typeof(Bomber), 100);
            AddUnitIfCapacityAllows("Tanker", typeof(Tanker), 80);

            if (baseStructure != null && baseStructure.HasShipyard)
            {
                AddUnitIfCapacityAllows("Patrol Boat", typeof(PatrolBoat), 40);
                AddUnitIfCapacityAllows("Destroyer", typeof(Destroyer), 80);
                AddUnitIfCapacityAllows("Submarine", typeof(Submarine), 90);
                AddUnitIfCapacityAllows("Carrier", typeof(Carrier), 120);
                AddUnitIfCapacityAllows("Battleship", typeof(Battleship), 150);
                AddUnitIfCapacityAllows("Transport", typeof(Transport), 60);
            }
        }

        private void ClearSelection()
        {
            selectedUnit = null;
            selectedStructure = null;
            UnitInfoPanel.Visibility = Visibility.Collapsed;
            StructureInfoPanel.Visibility = Visibility.Collapsed;
        }

        // AUTO-LANDING AND UNIT STACKING UPDATES
        // ========================================

        // UPDATED MoveUnit METHOD - Replace your existing MoveUnit method with this:

        private void MoveUnit(Unit unit, TilePosition targetPos)
        {
            // Calculate the path
            var path = game.Map.FindPath(unit.Position, targetPos, unit);

            if (path.Count == 0)
            {
                MessageBox.Show("No valid path to target!");
                return;
            }

            // Calculate movement cost with diagonal consideration
            double movementCost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var tile = game.Map.GetTile(path[i]);
                double cost = tile.GetMovementCost(unit);

                // Check if this is a diagonal move
                //if (i > 0)
                //{
                //    var prevPos = path[i - 1];
                //    var currPos = path[i];
                //    int dx = Math.Abs(currPos.X - prevPos.X);
                //    int dy = Math.Abs(currPos.Y - prevPos.Y);

                //    if (dx == 1 && dy == 1) // Diagonal
                //    {
                //        cost *= 1.414; // √2 approximation
                //    }
                //}

                movementCost += cost;
            }

            // Check if unit has enough movement points
            if (movementCost > unit.MovementPoints)
            {
                MessageBox.Show($"Not enough movement! Need {movementCost:F2}, have {unit.MovementPoints:F2}");
                return;
            }

            // Check if destination tile can accept this unit
            var destinationTile = game.Map.GetTile(targetPos);
            if (!destinationTile.CanUnitEnter(unit))
            {
                MessageBox.Show("Unit cannot enter that terrain type!");
                return;
            }

            // Check for enemy units at destination (cannot stack with enemies)
            var enemyUnits = destinationTile.Units.Where(u => u.OwnerId != unit.OwnerId).ToList();
            if (enemyUnits.Count > 0)
            {
                MessageBox.Show("Cannot move onto enemy units! Use attack instead.");
                return;
            }

            // NEW: Check for stack limit (maximum 3 units per tile)
            var friendlyUnits = destinationTile.Units.Where(u => u.OwnerId == unit.OwnerId).ToList();

            // Only check stack limit if destination doesn't have a structure
            // (structures can hold more units in their facilities)
            if (destinationTile.Structure == null && friendlyUnits.Count >= MAX_UNITS_PER_TILE)
            {
                MessageBox.Show("Cannot stack more than 3 units on a tile!");
                return;
            }

            // Move the unit
            var oldTile = game.Map.GetTile(unit.Position);
            oldTile.Units.Remove(unit);

            unit.Position = targetPos;
            unit.MovementPoints -= movementCost;

            var newTile = game.Map.GetTile(targetPos);
            newTile.Units.Add(unit);

            // AUTO-LANDING: Check if this is an air unit landing on a friendly base/city
            if (unit is AirUnit airUnit && newTile.Structure != null &&
                newTile.Structure.OwnerId == unit.OwnerId)
            {
                if (newTile.Structure is Base baseStructure)
                {
                    if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                    {
                        // Auto-land the aircraft
                        newTile.Units.Remove(airUnit);
                        baseStructure.Airport.Add(airUnit);
                        airUnit.HomeBaseId = baseStructure.StructureId;
                        airUnit.Fuel = airUnit.MaxFuel; // Refuel

                        MessageBox.Show($"{airUnit.GetName()} automatically landed and refueled at {baseStructure.GetName()}");

                        ClearSelection();
                        SelectStructure(baseStructure);
                    }
                    else
                    {
                        MessageBox.Show($"{airUnit.GetName()} moved to base, but airport is full! Use 'Land at Base' button to land.");
                    }
                }
                else if (newTile.Structure is City city)
                {
                    if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                    {
                        // Auto-land the aircraft
                        newTile.Units.Remove(airUnit);
                        city.Airport.Add(airUnit);
                        airUnit.HomeBaseId = city.StructureId;
                        airUnit.Fuel = airUnit.MaxFuel; // Refuel

                        MessageBox.Show($"{airUnit.GetName()} automatically landed and refueled at {city.GetName()}");

                        ClearSelection();
                        SelectStructure(city);
                    }
                    else
                    {
                        MessageBox.Show($"{airUnit.GetName()} moved to city, but airport is full! Use 'Land at Base' button to land.");
                    }
                }
            }

            // Update vision for the current player
            game.CurrentPlayer.UpdateVision(game.Map);

            // Update the selected unit display
            if (selectedUnit == unit)
            {
                SelectUnit(unit);
            }

            RenderMap();
            UpdateNextButton();
        }


        // PART 3: UPDATED
        // METHOD - Replace your entire method with this:

        private void ShowUnitStackSelection(List<Unit> units, TilePosition position)
        {
            // Create a selection window
            var selectionWindow = new Window
            {
                Title = $"Select Unit at ({position.X}, {position.Y})",
                Width = 350,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            var label = new TextBlock
            {
                Text = $"{units.Count} units at this location (max 3):",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(label);

            // Show warning if at stack limit
            if (units.Count >= MAX_UNITS_PER_TILE)
            {
                var warningLabel = new TextBlock
                {
                    Text = "⚠ Stack limit reached!",
                    Foreground = System.Windows.Media.Brushes.Red,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(warningLabel);
            }

            var listBox = new ListBox { Height = 180, Margin = new Thickness(0, 0, 0, 10) };

            foreach (var unit in units)
            {
                string unitInfo = $"{unit.GetName()} ({(unit.IsVeteran ? "Veteran" : "Regular")}) - " +
                                 $"Life: {unit.Life}/{unit.MaxLife}, Moves: {unit.MovementPoints:F1}/{unit.MaxMovementPoints}";

                if (unit is AirUnit airUnit)
                {
                    unitInfo += $", Fuel: {airUnit.Fuel}/{airUnit.MaxFuel}";
                }

                var item = new ListBoxItem
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

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var selectButton = new Button
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

            var cancelButton = new Button
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

            // Calculate actual path distance to nearest base
            int distance = airUnit.GetDistanceToNearestBase(game.Map, game.CurrentPlayer);

            if (distance >= 0)
            {
                FuelDistanceText.Text = $"Distance to nearest base: {distance} tiles (actual path)";
                FuelDistanceText.Foreground = System.Windows.Media.Brushes.DarkBlue;

                // Show warning if fuel is less than distance needed
                if (airUnit.Fuel < distance)
                {
                    int shortage = distance - airUnit.Fuel;
                    FuelWarningText.Text = $"⚠ STRANDED! Need {shortage} more fuel to reach base";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.Red;
                    FuelWarningText.Visibility = Visibility.Visible;
                }
                else if (airUnit.Fuel < distance + 2)
                {
                    int margin = airUnit.Fuel - distance;
                    FuelWarningText.Text = $"⚠ Low fuel margin! Only {margin} fuel to spare";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                    FuelWarningText.Visibility = Visibility.Visible;
                }
                else if (airUnit.Fuel < distance + 4)
                {
                    int margin = airUnit.Fuel - distance;
                    FuelWarningText.Text = $"Fuel margin: {margin} tiles (adequate)";
                    FuelWarningText.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
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

        private void HandleBomberTargetSelection(TilePosition targetPos)
        {
            // Calculate path and validate range
            var path = game.Map.FindPath(bomberForMission.Position, targetPos, bomberForMission);

            if (path.Count == 0)
            {
                MessageBox.Show("No valid path to target!");
                return;
            }

            int totalDistance = path.Count * 2; // Round trip
            if (totalDistance > bomberForMission.MaxFuel)
            {
                MessageBox.Show("Target out of range!");
                return;
            }

            // Set up the bombing run
            bomberForMission.TargetPosition = targetPos;
            bomberForMission.FlightPath = path;
            bomberForMission.CurrentOrders.Type = OrderType.BombingRun;

            MessageBox.Show($"Bombing mission set to {targetPos.X}, {targetPos.Y}");

            isSelectingBomberTarget = false;
            bomberForMission = null;
            BomberMissionPanel.Visibility = Visibility.Collapsed;
        }

        // Button Click Handlers
        private void MoveButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Right-click on map to move unit");
        }

        private void PatrolButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Patrol not yet implemented");
        }

        private void SentryButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.SetSentry();
                MessageBox.Show($"{selectedUnit.GetName()} is on sentry. It will wake when enemies are spotted.");

                // Update next button count
                UpdateNextButton();

                // Clear selection
                ClearSelection();
                RenderMap();
            }
        }


        private void AttackButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Attack not yet implemented");
        }

        private void BuildUnitButton_Click(object sender, RoutedEventArgs e)
        {
            if (UnitTypesCombo.SelectedItem == null)
                return;

            var selectedItem = (ComboBoxItem)UnitTypesCombo.SelectedItem;

            if (!selectedItem.IsEnabled)
            {
                MessageBox.Show("Cannot build this unit - facility is at capacity!");
                return;
            }

            dynamic tag = selectedItem.Tag;

            // Check capacity one more time before adding to queue
            bool canBuild = false;
            if (selectedStructure is Base baseStructure)
            {
                canBuild = baseStructure.CanBuildUnit(tag.Type);
            }
            else if (selectedStructure is City city)
            {
                canBuild = city.CanBuildUnit(tag.Type);
            }

            if (!canBuild)
            {
                MessageBox.Show("Cannot build this unit - facility is at capacity!");
                return;
            }

            var order = new UnitProductionOrder(tag.Type, tag.Cost, selectedItem.Content.ToString().Split('(')[0].Trim());

            if (selectedStructure is Base baseStruct)
            {
                baseStruct.ProductionQueue.Enqueue(order);
                UpdateProductionQueue(baseStruct);
                UpdateStructureLists(baseStruct);
                PopulateAvailableUnits(baseStruct);
            }
            else if (selectedStructure is City cityStruct)
            {
                cityStruct.ProductionQueue.Enqueue(order);
                UpdateProductionQueue(cityStruct);
                UpdateStructureLists(cityStruct);
                PopulateAvailableUnits(cityStruct);
            }
        }

        private void LaunchMissionButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Click on map to select bomber target");
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
            
            // Reset unit/structure indices for next turn
            currentUnitIndex = 0;
            currentStructureIndex = 0;

            game.NextTurn();

            // If the new current player is AI, execute their turn
            while (game.CurrentPlayer.IsAI)
            {
                // Show AI thinking panel
                AIThinkingPanel.Visibility = Visibility.Visible;
                AIStatusText.Text = $"{game.CurrentPlayer.Name} is planning...";
                NextUnitButton.Visibility = Visibility.Collapsed;
                UpdateEndTurnButtonImage();

                UpdateGameInfo();
                RenderMapFromHumanPerspective(); // Changed: always show from human player perspective

                // Let UI update
                await Task.Delay(500);

                // Execute AI turn with status updates
                await ExecuteAITurnWithUpdates(game.CurrentPlayer);

                game.NextTurn();
                UpdateEndTurnButtonImage();
            }

            // Hide AI panel when returning to player
            AIThinkingPanel.Visibility = Visibility.Collapsed;

            UpdateGameInfo();
            RenderMap();
            ClearSelection();
            UpdateNextButton();
        }

        private async Task ExecuteAITurnWithUpdates(Player aiPlayer)
        {
            AIStatusText.Text = "Building units...";
            await Task.Delay(300);

            AIStatusText.Text = "Moving units...";
            await Task.Delay(300);

            // Execute the actual AI logic
            aiController.ExecuteAITurn(aiPlayer);

            AIStatusText.Text = "Turn complete";
            RenderMapFromHumanPerspective(); // Changed: render from human perspective
            await Task.Delay(500);
        }

        private void RenderMapFromHumanPerspective()
        {
            // Always render from Player 0's perspective (the human player)
            var humanPlayer = game.Players[0];
            var bitmap = mapRenderer.RenderMap(humanPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };

            MapCanvas.Children.Clear();
            MapCanvas.Children.Add(image);
        }

        private void SaveGameButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Save not yet implemented");
        }

        private void LoadGameButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Load not yet implemented");
        }

        private void UpdateSubmarineDisplay(Submarine submarine)
        {
            SubmarinePanel.Visibility = Visibility.Visible;

            if (submarine.IsSubmerged)
            {
                SubmarineStatusText.Text = "🌊 Status: Submerged (Stealth Active)";
                SubmarineStatusText.Foreground = System.Windows.Media.Brushes.Navy;
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
            foreach (var aircraft in carrier.DockedAircraft)
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
            foreach (var embarkedUnit in transport.EmbarkedUnits)
            {
                CarrierContentsList.Items.Add($"{embarkedUnit.GetName()} - Life: {embarkedUnit.Life}/{embarkedUnit.MaxLife}");
            }
        }

        private void UpdatePatrolBoatDisplay(PatrolBoat patrolBoat)
        {
            // Check if in deep water
            var tile = game.Map.GetTile(patrolBoat.Position);
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
            foreach (var structure in game.CurrentPlayer.Structures)
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
                AntiAircraftProximityText.Foreground = System.Windows.Media.Brushes.DarkGreen;
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
                SpyStatusText.Foreground = System.Windows.Media.Brushes.DarkGreen;
            }

            SpyStatusText.Visibility = Visibility.Visible;
        }

        // NEW BUTTON HANDLERS - Add these methods:

        private void ReturnToBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is AirUnit airUnit)
            {
                var nearestBase = airUnit.GetNearestBase(game.Map, game.CurrentPlayer);
                if (nearestBase != null)
                {
                    // Set order to move to base
                    airUnit.CurrentOrders.Type = OrderType.MoveTo;
                    airUnit.CurrentOrders.TargetPosition = nearestBase.Position;
                    MessageBox.Show($"Aircraft ordered to return to {nearestBase.GetName()} at {nearestBase.Position.X}, {nearestBase.Position.Y}");
                }
                else
                {
                    MessageBox.Show("No friendly base found!");
                }
            }
        }

        private void LandAtBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is AirUnit airUnit)
            {
                // Check if adjacent to any friendly base
                var adjacentStructures = new List<Structure>();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        var checkPos = new TilePosition(airUnit.Position.X + dx, airUnit.Position.Y + dy);
                        if (game.Map.IsValidPosition(checkPos))
                        {
                            var tile = game.Map.GetTile(checkPos);
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
                    var structure = adjacentStructures[0];

                    // Remove from map
                    var tile = game.Map.GetTile(airUnit.Position);
                    tile.Units.Remove(airUnit);

                    // Add to airport
                    if (structure is Base baseStructure)
                    {
                        if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                        {
                            baseStructure.Airport.Add(airUnit);
                            airUnit.HomeBaseId = baseStructure.StructureId;
                            airUnit.Fuel = airUnit.MaxFuel; // Refuel
                            MessageBox.Show($"Aircraft landed and refueled at {structure.GetName()}");

                            ClearSelection();
                            SelectStructure(structure);
                            game.CurrentPlayer.UpdateVision(game.Map);
                            RenderMap();
                        }
                        else
                        {
                            tile.Units.Add(airUnit); // Put back on map
                            MessageBox.Show("Airport is full!");
                        }
                    }
                    else if (structure is City city)
                    {
                        if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                        {
                            city.Airport.Add(airUnit);
                            airUnit.HomeBaseId = city.StructureId;
                            airUnit.Fuel = airUnit.MaxFuel; // Refuel
                            MessageBox.Show($"Aircraft landed and refueled at {structure.GetName()}");

                            ClearSelection();
                            SelectStructure(structure);
                            game.CurrentPlayer.UpdateVision(game.Map);
                            RenderMap();
                        }
                        else
                        {
                            tile.Units.Add(airUnit); // Put back on map
                            MessageBox.Show("Airport is full!");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Not adjacent to a friendly base!");
                }
            }
        }

        private void ToggleSubmergeButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is Submarine submarine)
            {
                submarine.IsSubmerged = !submarine.IsSubmerged;
                UpdateSubmarineDisplay(submarine);
                MessageBox.Show(submarine.IsSubmerged ? "Submarine submerged" : "Submarine surfaced");
            }
        }

        private void SurrenderButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm surrender
            var result = MessageBox.Show(
                "Are you sure you want to surrender?\n\n" +
                "This will reveal the entire map showing all units and structures.\n" +
                "You will no longer be able to issue commands.",
                "Surrender?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Reveal the entire map
                game.RevealEntireMap();

                // Update the display
                RenderMap();

                // Disable game controls
                DisableGameControls();

                // Show surrender message
                MessageBox.Show(
                    "You have surrendered.\n\n" +
                    "The entire map is now visible.\n" +
                    "Red units belong to your opponents.\n\n" +
                    "Click 'New Game' to play again.",
                    "Surrendered",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void DisableGameControls()
        {
            // Disable unit commands
            if (MoveButton != null) MoveButton.IsEnabled = false;
            if (PatrolButton != null) PatrolButton.IsEnabled = false;
            if (AttackButton != null) AttackButton.IsEnabled = false;
            if (SkipTurnButton != null) SkipTurnButton.IsEnabled = false;
            if (SleepButton != null) SleepButton.IsEnabled = false;
            if (SentryButton != null) SentryButton.IsEnabled = false;
            if (WakeUpButton != null) WakeUpButton.IsEnabled = false;

            // Disable structure commands
            if (BuildUnitButton != null) BuildUnitButton.IsEnabled = false;
            if (TakeOffButton != null) TakeOffButton.IsEnabled = false;
            if (LandAtBaseButton != null) LandAtBaseButton.IsEnabled = false;
            if (ReturnToBaseButton != null) ReturnToBaseButton.IsEnabled = false;
            if (BombingRunButton != null) BombingRunButton.IsEnabled = false;
            if (RepairAircraftButton != null) RepairAircraftButton.IsEnabled = false;
            if (LaunchShipButton != null) LaunchShipButton.IsEnabled = false;
            if (RepairShipButton != null) RepairShipButton.IsEnabled = false;
            if (DeployMotorButton != null) DeployMotorButton.IsEnabled = false;
            if (DeployArmyButton != null) DeployArmyButton.IsEnabled = false;
            if (ToggleSubmergeButton != null) ToggleSubmergeButton.IsEnabled = false;

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
            if (MoveButton != null) MoveButton.IsEnabled = true;
            if (PatrolButton != null) PatrolButton.IsEnabled = true;
            if (AttackButton != null) AttackButton.IsEnabled = true;
            if (SkipTurnButton != null) SkipTurnButton.IsEnabled = true;
            if (SleepButton != null) SleepButton.IsEnabled = true;
            if (SentryButton != null) SentryButton.IsEnabled = true;

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
            // Confirm new game
            var result = MessageBox.Show(
                "Start a new game?",
                "New Game",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Restart the game
                InitializeGame();

                // Re-enable controls
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
    }
}