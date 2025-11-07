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


        public MainWindow()
        {
            InitializeComponent();

            // Show start game form
            var startForm = new StartGameForm();
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

            // Set player name
            game.Players[0].Name = gameSettings.CommanderName;

            GenerateMap();

            mapRenderer = new MapRenderer(game, TILE_SIZE);

            aiController = new AIController(game);

            foreach (var player in game.Players)
            {
                player.UpdateVision(game.Map);
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
                    var tile = game.Map.GetTile(new TilePosition(x, y));
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
                AddMessage($"{selectedUnit.GetName()} skipped for this turn", MessageType.Info);

                UpdateNextButton();
                ClearSelection();
                RenderMap();
            }
        }

        private void SleepButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit != null)
            {
                selectedUnit.Sleep();
                AddMessage($"{selectedUnit.GetName()} is now asleep. Select it to wake it up.", MessageType.Info);

                UpdateNextButton();
                ClearSelection();
                RenderMap();
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
            // Place at least 10-15 oil tiles
            int oilTiles = rand.Next(12, 18);
            PlaceResourceType(ResourceType.Oil, oilTiles, rand);

            // Place at least 10-15 steel tiles
            int steelTiles = rand.Next(12, 18);
            PlaceResourceType(ResourceType.Steel, steelTiles, rand);
        }

        private void PlaceResourceType(ResourceType resourceType, int count, Random rand)
        {
            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 100;

            // Preferred terrain for each resource
            List<TerrainType> preferredTerrain;
            List<TerrainType> fallbackTerrain;

            if (resourceType == ResourceType.Oil)
            {
                preferredTerrain = new List<TerrainType> { TerrainType.Plains, TerrainType.Land };
                fallbackTerrain = new List<TerrainType> { TerrainType.Forest, TerrainType.Hills };
            }
            else // Steel
            {
                preferredTerrain = new List<TerrainType> { TerrainType.Hills, TerrainType.Mountain };
                fallbackTerrain = new List<TerrainType> { TerrainType.Plains, TerrainType.Land, TerrainType.Forest };
            }

            // First pass - try preferred terrain with spacing
            while (placed < count && attempts < maxAttempts)
            {
                attempts++;

                int x = rand.Next(10, game.Map.Width - 10);
                int y = rand.Next(10, game.Map.Height - 10);
                var pos = new TilePosition(x, y);
                var tile = game.Map.GetTile(pos);

                if (tile.Resource != ResourceType.None)
                    continue;

                if (!preferredTerrain.Contains(tile.Terrain))
                    continue;

                if (!IsWellSpacedFromOtherResources(pos, 8))
                    continue;

                tile.Resource = resourceType;
                placed++;
            }

            // Second pass - use fallback terrain with spacing
            attempts = 0;
            while (placed < count && attempts < maxAttempts)
            {
                attempts++;

                int x = rand.Next(10, game.Map.Width - 10);
                int y = rand.Next(10, game.Map.Height - 10);
                var pos = new TilePosition(x, y);
                var tile = game.Map.GetTile(pos);

                if (tile.Resource != ResourceType.None)
                    continue;

                if (!fallbackTerrain.Contains(tile.Terrain))
                    continue;

                if (!IsWellSpacedFromOtherResources(pos, 5))  // Reduced spacing
                    continue;

                tile.Resource = resourceType;
                placed++;
            }

            // Third pass - ANY land terrain with minimal spacing (guarantee minimum)
            attempts = 0;
            var anyLandTerrain = new List<TerrainType>
    {
        TerrainType.Plains, TerrainType.Land, TerrainType.Forest,
        TerrainType.Hills, TerrainType.Mountain
    };

            while (placed < 10 && attempts < maxAttempts) // Guarantee at least 10
            {
                attempts++;

                int x = rand.Next(10, game.Map.Width - 10);
                int y = rand.Next(10, game.Map.Height - 10);
                var pos = new TilePosition(x, y);
                var tile = game.Map.GetTile(pos);

                if (tile.Resource != ResourceType.None)
                    continue;

                if (!anyLandTerrain.Contains(tile.Terrain))
                    continue;

                if (!IsWellSpacedFromOtherResources(pos, 3))  // Very minimal spacing
                    continue;

                tile.Resource = resourceType;
                placed++;
            }

            // Fourth pass - FORCE placement if still under 10 (no spacing requirement)
            attempts = 0;
            while (placed < 10 && attempts < maxAttempts * 2)
            {
                attempts++;

                int x = rand.Next(10, game.Map.Width - 10);
                int y = rand.Next(10, game.Map.Height - 10);
                var pos = new TilePosition(x, y);
                var tile = game.Map.GetTile(pos);

                if (tile.Resource != ResourceType.None)
                    continue;

                if (!anyLandTerrain.Contains(tile.Terrain))
                    continue;

                // No spacing check - just place it!
                tile.Resource = resourceType;
                placed++;
            }
        }

        private bool IsWellSpacedFromOtherResources(TilePosition pos, int minDistance)
        {
            for (int dx = -minDistance; dx <= minDistance; dx++)
            {
                for (int dy = -minDistance; dy <= minDistance; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    var checkPos = new TilePosition(pos.X + dx, pos.Y + dy);
                    if (game.Map.IsValidPosition(checkPos))
                    {
                        var tile = game.Map.GetTile(checkPos);
                        if (tile.Resource != ResourceType.None)
                            return false; // Too close to another resource
                    }
                }
            }
            return true;
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
            // ALWAYS render from human player's perspective (Player 0)
            Player humanPlayer = game.Players[0];

            var bitmap = mapRenderer.RenderMap(humanPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            MapCanvas.Children.Clear();

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };

            Canvas.SetZIndex(image, 10);
            MapCanvas.Children.Add(image);
        }

        private void RenderResourceIcons(Player renderPlayer)
        {
            int iconsAdded = 0;

            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);

                    VisibilityLevel visibility = VisibilityLevel.Hidden;
                    if (renderPlayer.FogOfWar.ContainsKey(pos))
                    {
                        visibility = renderPlayer.FogOfWar[pos];
                    }

                    if (visibility == VisibilityLevel.Visible && tile.Resource != ResourceType.None)
                    {
                        try
                        {
                            var resourceImage = new System.Windows.Controls.Image
                            {
                                Width = 20,
                                Height = 20
                            };

                            string imagePath = "";
                            if (tile.Resource == ResourceType.Oil)
                            {
                                imagePath = "/Resources/oil_16.png";
                            }
                            else if (tile.Resource == ResourceType.Steel)
                            {
                                imagePath = "/Resources/steel_16.png";
                            }

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

            var clickPos = e.GetPosition(MapCanvas);
            var tilePos = new TilePosition((int)(clickPos.X / TILE_SIZE), (int)(clickPos.Y / TILE_SIZE));

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

            // NEW: Handle patrol waypoint selection
            if (isSelectingPatrolWaypoints)
            {
                HandlePatrolWaypointSelection(tilePos);
                return;
            }

            var tile = game.Map.GetTile(tilePos);

            // Check for units at this position (exclude satellites - they're untouchable)
            var friendlyUnits = tile.Units
                .Where(u => u.OwnerId == game.CurrentPlayer.PlayerId && !(u is Satellite))
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

            // Clear selection
            ClearSelection();
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
            var fullRoute = new List<TilePosition>();
            fullRoute.Add(patrolStartPosition);  // Index 0 - Start

            // Add all waypoints (going out)
            foreach (var wp in patrolWaypoints)
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
            var patrolOrder = new AutomaticOrder(unitOnPatrol, patrolStartPosition, AutomaticOrderType.Patrol);
            patrolOrder.PatrolWaypoints = fullRoute;

            // Start at waypoint 1, not 0, since unit is already at position 0
            patrolOrder.CurrentWaypointIndex = 1;

            game.AutomaticOrdersQueue.Enqueue(patrolOrder);

            // Handle aircraft takeoff if needed
            if (unitOnPatrol is AirUnit airUnit && airUnit.HomeBaseId != -1)
            {
                Structure homeBase = null;
                foreach (var structure in game.CurrentPlayer.Structures)
                {
                    if (structure.StructureId == airUnit.HomeBaseId)
                    {
                        homeBase = structure;
                        break;
                    }
                }

                if (homeBase != null)
                {
                    var adjacentPos = FindAdjacentEmptyTile(homeBase.Position);
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

                        var tile = game.Map.GetTile(adjacentPos);
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

            var adjacentPos = FindAdjacentWaterTile(selectedStructure.Position);

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

            var tile = game.Map.GetTile(adjacentPos);
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

            var adjacentPos = FindAdjacentEmptyTile(selectedStructure.Position);

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

            var tile = game.Map.GetTile(adjacentPos);
            tile.Units.Add(aircraft);

            AddMessage($"{aircraft.GetName()} took off from {selectedStructure.GetName()}", MessageType.Movement);

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
            int index = 0;
            foreach (var order in baseStructure.ProductionQueue)
            {
                if (index == 0)
                {
                    // First item - show actual progress
                    ProductionQueueList.Items.Add($"▶ {order.DisplayName} ({baseStructure.CurrentProductionProgress}/{order.TotalCost})");
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
            foreach (var order in city.ProductionQueue)
            {
                if (index == 0)
                {
                    // First item - show actual progress
                    ProductionQueueList.Items.Add($"▶ {order.DisplayName} ({city.CurrentProductionProgress}/{order.TotalCost})");
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

            var baseStructure = structure as Base;
            var city = structure as City;
            var player = game.CurrentPlayer;

            void AddUnit(string name, Type type, int gold, int steel, int oil)
            {
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

                // Check resources
                bool canAfford = player.Gold >= gold &&
                                player.Steel >= steel &&
                                player.Oil >= oil;

                // Build cost string with color indicators
                string costString = $"{name} (";

                // Gold
                costString += player.Gold >= gold ? $"💰{gold}" : $"💰{gold}[!]";

                // Steel
                if (steel > 0)
                    costString += player.Steel >= steel ? $" ⚙️{steel}" : $" ⚙️{steel}[!]";

                // Oil
                if (oil > 0)
                    costString += player.Oil >= oil ? $" 🛢️{oil}" : $" 🛢️{oil}[!]";

                costString += ")";

                if (!canAfford)
                    costString += " [Need Resources]";
                else if (canBuild)
                    costString += " ✓";

                costString += capacityNote;

                var item = new ComboBoxItem
                {
                    Content = costString,
                    Tag = new UnitProductionOrder(type, gold, steel, oil, name),
                    IsEnabled = canBuild && canAfford
                };

                // Color coding
                if (!canAfford)
                    item.Foreground = System.Windows.Media.Brushes.Red;
                else if (canBuild)
                    item.Foreground = System.Windows.Media.Brushes.DarkGreen;

                UnitTypesCombo.Items.Add(item);
            }

            void AddSatelliteUnit(string name, Type type, int gold, int steel, int oil, OrbitType orbitType)
            {
                // Satellites can be built at bases and cities (no capacity limit)
                bool canBuild = (baseStructure != null || city != null);

                // Check resources
                bool canAfford = player.Gold >= gold &&
                                player.Steel >= steel &&
                                player.Oil >= oil;

                // Build cost string with color indicators
                string costString = $"{name} (";

                // Gold
                costString += player.Gold >= gold ? $"💰{gold}" : $"💰{gold}[!]";

                // Steel
                if (steel > 0)
                    costString += player.Steel >= steel ? $" ⚙️{steel}" : $" ⚙️{steel}[!]";

                // Oil
                if (oil > 0)
                    costString += player.Oil >= oil ? $" 🛢️{oil}" : $" 🛢️{oil}[!]";

                costString += ")";

                if (!canAfford)
                    costString += " [Need Resources]";
                else if (canBuild)
                    costString += " ✓";

                var item = new ComboBoxItem
                {
                    Content = costString,
                    Tag = new SatelliteProductionOrder(type, gold, steel, oil, name, orbitType),
                    IsEnabled = canBuild && canAfford
                };

                // Color coding
                if (!canAfford)
                    item.Foreground = System.Windows.Media.Brushes.Red;
                else if (canBuild)
                    item.Foreground = System.Windows.Media.Brushes.DarkGreen;

                UnitTypesCombo.Items.Add(item);
            }

            // Add all units with resource costs
            AddUnit("Army", typeof(Army), 2, 0, 0);
            AddUnit("Tank", typeof(Tank), 2, 1, 0);
            AddUnit("Artillery", typeof(Artillery), 2, 1, 0);
            AddUnit("Anti-Aircraft", typeof(AntiAircraft), 2, 1, 0);
            AddUnit("Spy", typeof(Spy), 3, 0, 0);
            AddUnit("Fighter", typeof(Fighter), 3, 1, 1);
            AddUnit("Bomber", typeof(Bomber), 4, 2, 1);
            AddUnit("Tanker", typeof(Tanker), 3, 1, 1);

            // Add Orbiting Satellites - one entry for each orbit type that player hasn't deployed yet
            if (!player.DeployedOrbitTypes.Contains(OrbitType.Horizontal))
                AddSatelliteUnit("Orbit Sat (Horizontal)", typeof(OrbitingSatellite), 6, 3, 2, OrbitType.Horizontal);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.Vertical))
                AddSatelliteUnit("Orbit Sat (Vertical)", typeof(OrbitingSatellite), 6, 3, 2, OrbitType.Vertical);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.RightDiagonal))
                AddSatelliteUnit("Orbit Sat (Right Diag)", typeof(OrbitingSatellite), 6, 3, 2, OrbitType.RightDiagonal);
            if (!player.DeployedOrbitTypes.Contains(OrbitType.LeftDiagonal))
                AddSatelliteUnit("Orbit Sat (Left Diag)", typeof(OrbitingSatellite), 6, 3, 2, OrbitType.LeftDiagonal);

            // Add Geosynchronous Satellite (no restrictions)
            AddUnit("Geosync Satellite", typeof(GeosynchronousSatellite), 10, 5, 4);

            if (baseStructure != null && baseStructure.HasShipyard)
            {
                AddUnit("Patrol Boat", typeof(PatrolBoat), 2, 1, 0);
                AddUnit("Destroyer", typeof(Destroyer), 3, 2, 1);
                AddUnit("Submarine", typeof(Submarine), 3, 1, 1);
                AddUnit("Carrier", typeof(Carrier), 5, 3, 2);
                AddUnit("Battleship", typeof(Battleship), 5, 3, 2);
                AddUnit("Transport", typeof(Transport), 2, 1, 1);
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
            var path = game.Map.FindPath(unit.Position, targetPos, unit);

            if (path.Count == 0)
            {
                AddMessage("No valid path to target!", MessageType.Warning);
                return;
            }

            double movementCost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var tile = game.Map.GetTile(path[i]);
                double cost = tile.GetMovementCost(unit);
                movementCost += cost;
            }

            if (movementCost > unit.MovementPoints)
            {
                AddMessage($"Not enough movement! Need {movementCost:F2}, have {unit.MovementPoints:F2}", MessageType.Warning);
                return;
            }

            var destinationTile = game.Map.GetTile(targetPos);
            if (!destinationTile.CanUnitEnter(unit))
            {
                AddMessage("Unit cannot enter that terrain type!", MessageType.Warning);
                return;
            }

            var enemyUnits = destinationTile.Units.Where(u => u.OwnerId != unit.OwnerId).ToList();
            if (enemyUnits.Count > 0)
            {
                AddMessage("Cannot move onto enemy units! Use attack instead.", MessageType.Warning);
                return;
            }

            // Exclude satellites from stacking - they're in orbit and don't count
            var friendlyUnits = destinationTile.Units
                .Where(u => u.OwnerId == unit.OwnerId && !(u is Satellite))
                .ToList();

            if (destinationTile.Structure == null && friendlyUnits.Count >= MAX_UNITS_PER_TILE)
            {
                AddMessage("Cannot stack more than 3 units on a tile!", MessageType.Warning);
                return;
            }

            var oldTile = game.Map.GetTile(unit.Position);
            oldTile.Units.Remove(unit);

            unit.Position = targetPos;
            unit.MovementPoints -= movementCost;

            var newTile = game.Map.GetTile(targetPos);
            newTile.Units.Add(unit);

            // NEW: Claim tile ownership
            int previousOwner = newTile.OwnerId;
            newTile.OwnerId = unit.OwnerId;

            // Notify if we captured a resource tile
            if (newTile.Resource != ResourceType.None)
            {
                if (previousOwner == -1)
                {
                    AddMessage($"📍 Claimed {newTile.Resource} resource at ({targetPos.X}, {targetPos.Y})", MessageType.Success);
                }
                else if (previousOwner != unit.OwnerId)
                {
                    AddMessage($"⚔️ Captured {newTile.Resource} resource from {game.Players[previousOwner].Name}!", MessageType.Success);
                }
            }

            if (unit is AirUnit airUnit && newTile.Structure != null &&
                newTile.Structure.OwnerId == unit.OwnerId)
            {
                if (newTile.Structure is Base baseStructure)
                {
                    if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                    {
                        newTile.Units.Remove(airUnit);
                        baseStructure.Airport.Add(airUnit);
                        airUnit.HomeBaseId = baseStructure.StructureId;
                        airUnit.Fuel = airUnit.MaxFuel;

                        AddMessage($"{airUnit.GetName()} automatically landed and refueled at {baseStructure.GetName()}", MessageType.Success);

                        ClearSelection();
                        SelectStructure(baseStructure);
                    }
                    else
                    {
                        AddMessage($"{airUnit.GetName()} moved to base, but airport is full!", MessageType.Warning);
                    }
                }
                else if (newTile.Structure is City city)
                {
                    if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                    {
                        newTile.Units.Remove(airUnit);
                        city.Airport.Add(airUnit);
                        airUnit.HomeBaseId = city.StructureId;
                        airUnit.Fuel = airUnit.MaxFuel;

                        AddMessage($"{airUnit.GetName()} automatically landed and refueled at {city.GetName()}", MessageType.Success);

                        ClearSelection();
                        SelectStructure(city);
                    }
                    else
                    {
                        AddMessage($"{airUnit.GetName()} moved to city, but airport is full!", MessageType.Warning);
                    }
                }
            }

            game.CurrentPlayer.UpdateVision(game.Map);
            UpdateResourceDisplay();

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
            // Filter out satellites - they're untouchable and in orbit
            units = units.Where(u => !(u is Satellite)).ToList();

            // If no selectable units after filtering, return
            if (units.Count == 0)
                return;

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

            // Count only stackable units (satellites don't count)
            int stackableUnits = units.Count(u => !(u is Satellite));

            var label = new TextBlock
            {
                Text = $"{stackableUnits} units at this location (max 3):",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(label);

            // Show warning if at stack limit
            if (stackableUnits >= MAX_UNITS_PER_TILE)
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

            // Calculate actual path distance to nearest base (in tiles)
            int distanceInTiles = airUnit.GetDistanceToNearestBase(game.Map, game.CurrentPlayer);

            if (distanceInTiles >= 0)
            {
                // Convert tile distance to turns needed
                // Each turn the aircraft moves MaxMovementPoints tiles and consumes 1 fuel
                int turnsNeeded = (int)Math.Ceiling((double)distanceInTiles / airUnit.MaxMovementPoints);

                FuelDistanceText.Text = $"Distance to nearest base: {distanceInTiles} tiles ({turnsNeeded} turn{(turnsNeeded != 1 ? "s" : "")})";
                FuelDistanceText.Foreground = System.Windows.Media.Brushes.DarkBlue;

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

            var tile = game.Map.GetTile(tilePos);

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

            // Update vision immediately
            game.CurrentPlayer.UpdateVision(game.Map);

            // Render the map to show the satellite
            RenderMap();
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
                    foreach (var structure in game.CurrentPlayer.Structures)
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
                MessageBox.Show("Cannot build this unit - either at capacity or insufficient resources!");
                return;
            }

            // The tag already contains a properly constructed UnitProductionOrder
            var order = (UnitProductionOrder)selectedItem.Tag;

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
                MessageBox.Show("Cannot build this unit - facility is at capacity!");
                return;
            }

            // Check if player has sufficient resources
            if (game.CurrentPlayer.Gold < order.GoldCost ||
                game.CurrentPlayer.Steel < order.SteelCost ||
                game.CurrentPlayer.Oil < order.OilCost)
            {
                MessageBox.Show($"Insufficient resources!\n\nRequired: 💰{order.GoldCost} ⚙️{order.SteelCost} 🛢️{order.OilCost}\nAvailable: 💰{game.CurrentPlayer.Gold} ⚙️{game.CurrentPlayer.Steel} 🛢️{game.CurrentPlayer.Oil}");
                return;
            }

            // Deduct resources immediately when adding to queue
            game.CurrentPlayer.Gold -= order.GoldCost;
            game.CurrentPlayer.Steel -= order.SteelCost;
            game.CurrentPlayer.Oil -= order.OilCost;

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

            AddMessage($"Resources paid! 💰{order.GoldCost} ⚙️{order.SteelCost} 🛢️{order.OilCost}\n\n{order.DisplayName} added to production queue.");
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

            currentUnitIndex = 0;
            currentStructureIndex = 0;

            game.NextTurn();

            // Display any production messages
            while (game.ProductionMessages.Count > 0)
            {
                var message = game.ProductionMessages.Dequeue();
                AddMessage(message, MessageType.Success);
            }

            AddMessage($"=== Turn {game.TurnNumber} begins ===", MessageType.Success);

            if (!game.CurrentPlayer.IsAI)
            {
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

                // Execute AI turn (no rendering)
                aiController.ExecuteAITurn(game.CurrentPlayer);

                game.NextTurn();

                // Display any production messages from AI turns too
                while (game.ProductionMessages.Count > 0)
                {
                    var message = game.ProductionMessages.Dequeue();
                    AddMessage(message, MessageType.Success);
                }

                // For AI players, auto-place any unplaced geosync satellites
                if (game.CurrentPlayer.IsAI)
                {
                    var unplacedGeosync = game.CurrentPlayer.Units
                        .OfType<GeosynchronousSatellite>()
                        .FirstOrDefault(s => s.Position.X == -1 || s.Position.Y == -1);

                    if (unplacedGeosync != null)
                    {
                        // Place at AI's first base/city
                        var aiStructure = game.CurrentPlayer.Structures.FirstOrDefault();
                        if (aiStructure != null)
                        {
                            unplacedGeosync.Position = aiStructure.Position;
                            var tile = game.Map.GetTile(aiStructure.Position);
                            tile.Units.Add(unplacedGeosync);
                        }
                    }
                }

                if (!game.CurrentPlayer.IsAI)
                {
                    await ProcessAutomaticOrdersWithVisuals();
                }

                UpdateEndTurnButtonImage();
            }

            // AI turns complete - now render once for human player
            AIThinkingPanel.Visibility = Visibility.Collapsed;

            // ADD THIS CHECK HERE - AFTER AI LOOP, WHEN IT'S HUMAN'S TURN AGAIN
            if (!game.CurrentPlayer.IsAI)
            {
                var unplacedGeosync = game.CurrentPlayer.Units
                    .OfType<GeosynchronousSatellite>()
                    .FirstOrDefault(s => s.Position.X == -1 || s.Position.Y == -1);

                if (unplacedGeosync != null)
                {
                    isSelectingGeosyncLocation = true;
                    geosyncToPlace = unplacedGeosync;
                    AddMessage("🛰️ Geosynchronous Satellite ready! Click on map to deploy.", MessageType.Info);
                }
            }

            UpdateGameInfo();
            RenderMap();
            ClearSelection();
            UpdateNextButton();
            UpdateResourceDisplay();
        }

        private void RenderMapFromHumanPerspective()
        {
            Player humanPlayer = game.Players[0];
            var bitmap = mapRenderer.RenderMap(humanPlayer, selectedUnit, selectedStructure);

            MapCanvas.Width = bitmap.PixelWidth;
            MapCanvas.Height = bitmap.PixelHeight;

            MapCanvas.Children.Clear();

            var image = new System.Windows.Controls.Image
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

                var nearestBase = airUnit.GetNearestBase(game.Map, game.CurrentPlayer);
                if (nearestBase != null)
                {
                    game.AddAutomaticOrder(airUnit, nearestBase.Position, AutomaticOrderType.ReturnToBase);

                    AddMessage($"{airUnit.GetName()} ordered to return to {nearestBase.GetName()} at ({nearestBase.Position.X}, {nearestBase.Position.Y}).", MessageType.Movement);

                    await ProcessAutomaticOrdersWithVisuals();

                    game.CurrentPlayer.UpdateVision(game.Map);
                    RenderMap();

                    var tile = game.Map.GetTile(airUnit.Position);
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

        private void LandAtBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUnit is AirUnit airUnit)
            {
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

                    var tile = game.Map.GetTile(airUnit.Position);
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
        private void UpdateResourceDisplay()
        {
            var player = game.CurrentPlayer;

            // Get income
            var (goldIncome, steelIncome, oilIncome) = player.GetResourceIncome(game.Map);

            // Update display
            GoldText.Text = player.Gold.ToString();
            GoldIncomeText.Text = $"(+{goldIncome})";

            SteelText.Text = player.Steel.ToString();
            SteelIncomeText.Text = $"(+{steelIncome})";

            OilText.Text = player.Oil.ToString();
            OilIncomeText.Text = $"(+{oilIncome})";
        }

        private async Task ProcessAutomaticOrdersWithVisuals()
        {
            if (game.AutomaticOrdersQueue.Count == 0)
                return;

            var ordersToProcess = game.AutomaticOrdersQueue.ToList();
            game.AutomaticOrdersQueue.Clear();

            var enemiesSpottedUnits = new List<Unit>();

            foreach (var order in ordersToProcess)
            {
                // Skip if unit is dead or belongs to a different player
                if (order.Unit.Life <= 0 || order.Unit.OwnerId != game.CurrentPlayer.PlayerId)
                    continue;

                // Process the order with visual updates
                var (shouldContinue, enemySpotted) = await game.ProcessAutomaticOrder(
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
                MessageBox.Show($"⚠ ENEMY SPOTTED!\n\n{unitNames} detected enemy forces and halted automatic movement.\n\nThe unit(s) are now under your control.",
                                "Enemy Contact",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                // Select the first unit that spotted an enemy
                if (enemiesSpottedUnits.Count > 0)
                {
                    SelectUnit(enemiesSpottedUnits[0]);
                    CenterOnPosition(enemiesSpottedUnits[0].Position);
                }
            }
        }


        private void AddMessage(string text, MessageType type = MessageType.Info)
        {
            messageLog.AddMessage(text, type);
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

            public SatelliteProductionOrder(Type unitType, int goldCost, int steelCost, int oilCost,
                string displayName, OrbitType orbitType)
                : base(unitType, goldCost, steelCost, oilCost, displayName)
            {
                OrbitType = orbitType;
            }
        }
    }

}