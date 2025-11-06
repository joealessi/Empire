public class Game
{
    public Map Map { get; set; }
    public List<Player> Players { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public int TurnNumber { get; set; }

    private int nextUnitId = 1;
    private int nextStructureId = 1;
    public Queue<AutomaticOrder> AutomaticOrdersQueue { get; set; }

    public bool HasSurrendered { get; set; }

    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public Game(int mapWidth, int mapHeight, int playerCount)
    {
        Map = new Map(mapWidth, mapHeight);
        Players = new List<Player>();

        for (int i = 0; i < playerCount; i++)
        {
            Players.Add(new Player(i, $"Player {i + 1}", i > 0)); // Player 0 is human, rest are AI
        }

        CurrentPlayerIndex = 0;
        TurnNumber = 1;

        AutomaticOrdersQueue = new Queue<AutomaticOrder>();
    }

    public void NextTurn()
    {
        CurrentPlayerIndex++;
        if (CurrentPlayerIndex >= Players.Count)
        {
            CurrentPlayerIndex = 0;
            TurnNumber++;

            // Process turn-based mechanics
            ProcessTurnMechanics();
        }

        // Update vision for current player (moved outside the if block)
        CurrentPlayer.UpdateVision(Map);
    }

    private void ProcessTurnMechanics()
    {
        foreach (var player in Players)
        {
            // Check sentry units BEFORE processing their turn
            CheckSentryUnits(player);

            // NEW: Calculate and apply resource income
            player.CalculateResourceIncome(Map);

            // Consume fuel for air units
            foreach (var unit in player.Units)
            {
                if (unit is AirUnit airUnit)
                {
                    airUnit.ConsumeFuel();
                }

                // Restore movement points
                unit.MovementPoints = unit.MaxMovementPoints;

                // Clear skip flag for new turn
                unit.IsSkippedThisTurn = false;

                // Process automated orders
                ProcessUnitOrders(unit);
            }

            // Process production queues
            foreach (var structure in player.Structures)
            {
                if (structure is Base baseStructure)
                {
                    ProcessProduction(baseStructure, player);
                }
                else if (structure is City city)
                {
                    ProcessProduction(city, player);
                }
            }
        }
    }

    private void CheckSentryUnits(Player player)
    {
        foreach (var unit in player.Units)
        {
            if (unit.IsOnSentry)
            {
                // Get unit vision range
                int visionRange = GetUnitVisionRange(unit);

                // Check tiles in vision range for enemies
                var tilesInVision = Map.GetTilesInRadius(unit.Position, visionRange);

                foreach (var tile in tilesInVision)
                {
                    // Check if there are enemy units visible
                    var enemyUnits = tile.Units.Where(u => u.OwnerId != player.PlayerId).ToList();
                    if (enemyUnits.Count > 0 && Map.HasLineOfSight(unit.Position, tile.Position))
                    {
                        // Wake up the unit!
                        unit.WakeUp();
                        break;
                    }
                }
            }
        }
    }

    private int GetUnitVisionRange(Unit unit)
    {
        return unit switch
        {
            Fighter => 6,
            Bomber => 6,
            Tanker => 6,
            Carrier => 5,
            Battleship => 5,
            Destroyer => 4,
            Submarine => 3,
            PatrolBoat => 4,
            Transport => 3,
            Tank => 2,
            Artillery => 2,
            AntiAircraft => 2,
            Spy => 3,
            Army => 1,
            _ => 2
        };
    }

    private void ProcessUnitOrders(Unit unit)
    {
        if (unit.CurrentOrders.Type == OrderType.Patrol &&
            unit.CurrentOrders.PatrolWaypoints.Count > 0)
        {
            // Move unit along patrol route
            // Implementation details would go here
        }
        else if (unit is Bomber bomber &&
                 unit.CurrentOrders.Type == OrderType.BombingRun)
        {
            // Process bomber movement along flight path
            // Implementation details would go here
        }
    }

    private void ProcessProduction(Base baseStructure, Player player)
    {
        if (baseStructure.ProductionQueue.Count == 0)
            return;

        var currentOrder = baseStructure.ProductionQueue.Peek();
        baseStructure.CurrentProductionProgress += baseStructure.ProductionPointsPerTurn;

        if (baseStructure.CurrentProductionProgress >= currentOrder.TotalCost)
        {
            // Production complete - resources were already paid when queued
            var unit = CreateUnit(currentOrder.UnitType, baseStructure.Position, player.PlayerId);
            player.Units.Add(unit);

            // Place unit in appropriate storage (check capacity)
            bool placed = false;

            if (unit is AirUnit airUnit)
            {
                if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                {
                    baseStructure.Airport.Add(airUnit);
                    airUnit.HomeBaseId = baseStructure.StructureId;
                    placed = true;
                }
            }
            else if (unit is SeaUnit seaUnit)
            {
                if (baseStructure.Shipyard.Count < Base.MAX_SHIPYARD_CAPACITY)
                {
                    baseStructure.Shipyard.Add(seaUnit);
                    placed = true;
                }
            }
            else if (unit is Tank || unit is Artillery || unit is AntiAircraft)
            {
                baseStructure.MotorPool.Add(unit);
                placed = true;
            }
            else if (unit is Army army)
            {
                if (baseStructure.Barracks.Count < Base.MAX_BARRACKS_CAPACITY)
                {
                    baseStructure.Barracks.Add(army);
                    placed = true;
                }
            }

            // If couldn't place in storage, put on adjacent tile
            if (!placed)
            {
                var adjacentPos = FindAdjacentEmptyTile(baseStructure.Position);
                if (adjacentPos.X != -1)
                {
                    unit.Position = adjacentPos;
                    var tile = Map.GetTile(adjacentPos);
                    tile.Units.Add(unit);
                }
                else
                {
                    // No space at all - unit is lost
                    player.Units.Remove(unit);
                }
            }

            // Remove from queue and reset progress
            baseStructure.ProductionQueue.Dequeue();
            baseStructure.CurrentProductionProgress = 0;
        }

        // Process repairs
        ProcessRepairs(baseStructure);
    }

    private void ProcessProduction(City city, Player player)
    {
        if (city.ProductionQueue.Count == 0)
            return;

        var currentOrder = city.ProductionQueue.Peek();
        city.CurrentProductionProgress += city.ProductionPointsPerTurn;

        if (city.CurrentProductionProgress >= currentOrder.TotalCost)
        {
            // Production complete - resources were already paid when queued
            var unit = CreateUnit(currentOrder.UnitType, city.Position, player.PlayerId);
            player.Units.Add(unit);

            // Place unit in appropriate storage (check capacity)
            bool placed = false;

            if (unit is AirUnit airUnit)
            {
                if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                {
                    city.Airport.Add(airUnit);
                    airUnit.HomeBaseId = city.StructureId;
                    placed = true;
                }
            }
            else if (unit is Tank || unit is Artillery || unit is AntiAircraft)
            {
                city.MotorPool.Add(unit);
                placed = true;
            }
            else if (unit is Army army)
            {
                if (city.Barracks.Count < City.MAX_BARRACKS_CAPACITY)
                {
                    city.Barracks.Add(army);
                    placed = true;
                }
            }

            // If couldn't place in storage, put on adjacent tile
            if (!placed)
            {
                var adjacentPos = FindAdjacentEmptyTile(city.Position);
                if (adjacentPos.X != -1)
                {
                    unit.Position = adjacentPos;
                    var tile = Map.GetTile(adjacentPos);
                    tile.Units.Add(unit);
                }
                else
                {
                    // No space at all - unit is lost
                    player.Units.Remove(unit);
                }
            }

            // Remove from queue and reset progress
            city.ProductionQueue.Dequeue();
            city.CurrentProductionProgress = 0;
        }

        // Process repairs
        ProcessRepairs(city);
    }

    private void ProcessRepairs(Base baseStructure)
    {
        var completedRepairs = new List<Unit>();

        foreach (var kvp in baseStructure.UnitsBeingRepaired.ToList())
        {
            var unit = kvp.Key;
            var turnsRemaining = kvp.Value - 1;

            if (turnsRemaining <= 0)
            {
                // Repair complete
                unit.Life = unit.MaxLife;
                completedRepairs.Add(unit);
            }
            else
            {
                baseStructure.UnitsBeingRepaired[unit] = turnsRemaining;
            }
        }

        foreach (var unit in completedRepairs)
        {
            baseStructure.UnitsBeingRepaired.Remove(unit);
        }
    }

    private void ProcessRepairs(City city)
    {
        var completedRepairs = new List<Unit>();

        foreach (var kvp in city.UnitsBeingRepaired.ToList())
        {
            var unit = kvp.Key;
            var turnsRemaining = kvp.Value - 1;

            if (turnsRemaining <= 0)
            {
                // Repair complete
                unit.Life = unit.MaxLife;
                completedRepairs.Add(unit);
            }
            else
            {
                city.UnitsBeingRepaired[unit] = turnsRemaining;
            }
        }

        foreach (var unit in completedRepairs)
        {
            city.UnitsBeingRepaired.Remove(unit);
        }
    }

    private TilePosition FindAdjacentEmptyTile(TilePosition centerPos)
    {
        int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

        for (int i = 0; i < 8; i++)
        {
            var pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

            if (Map.IsValidPosition(pos))
            {
                var tile = Map.GetTile(pos);

                if (tile.Units.Count == 0 && tile.Structure == null)
                {
                    return pos;
                }
            }
        }

        return new TilePosition(-1, -1);
    }

    private Unit CreateUnit(Type unitType, TilePosition position, int ownerId)
    {
        var unit = (Unit)Activator.CreateInstance(unitType);
        unit.UnitId = nextUnitId++;
        unit.Position = position;
        unit.OwnerId = ownerId;
        return unit;
    }

    public Structure CreateStructure(Type structureType, TilePosition position, int ownerId)
    {
        var structure = (Structure)Activator.CreateInstance(structureType);
        structure.StructureId = nextStructureId++;
        structure.Position = position;
        structure.OwnerId = ownerId;
        return structure;
    }

    public void RevealEntireMap()
    {
        HasSurrendered = true;

        foreach (var player in Players)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                for (int y = 0; y < Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    player.FogOfWar[pos] = VisibilityLevel.Visible;
                }
            }
        }
    }
    public bool ProcessAutomaticOrder(AutomaticOrder order, Action<Unit> updateVisionCallback, Action renderCallback, out bool enemySpotted)
    {
        enemySpotted = false;

        if (order.Unit.Life <= 0)
        {
            // Unit destroyed, cancel order
            return false;
        }

        // Get the unit's owner
        var owner = Players.FirstOrDefault(p => p.PlayerId == order.Unit.OwnerId);
        if (owner == null)
            return false;

        // Recalculate path if needed (in case terrain changed or unit moved)
        if (order.CurrentPath.Count == 0 || order.PathIndex >= order.CurrentPath.Count)
        {
            order.CurrentPath = Map.FindPath(order.Unit.Position, order.Destination, order.Unit);
            order.PathIndex = 1; // Start at 1 since path[0] is current position

            if (order.CurrentPath.Count == 0)
            {
                // No path available, cancel order
                return false;
            }
        }

        // Move along path tile by tile
        while (order.PathIndex < order.CurrentPath.Count && order.Unit.MovementPoints >= 0.5)
        {
            var nextTile = Map.GetTile(order.CurrentPath[order.PathIndex]);
            double cost = nextTile.GetMovementCost(order.Unit);

            if (cost <= order.Unit.MovementPoints)
            {
                // Check if tile is blocked by friendly units (except destination)
                bool isDestination = order.CurrentPath[order.PathIndex].Equals(order.Destination);
                bool occupied = nextTile.Units.Any(u => u.OwnerId == order.Unit.OwnerId && u.UnitId != order.Unit.UnitId);

                if (occupied && !isDestination)
                {
                    // Path blocked, recalculate next turn
                    order.PathIndex = order.CurrentPath.Count; // Force recalculation
                    break;
                }

                // Move the unit
                var oldTile = Map.GetTile(order.Unit.Position);
                oldTile.Units.Remove(order.Unit);

                order.Unit.Position = order.CurrentPath[order.PathIndex];
                nextTile.Units.Add(order.Unit);

                order.Unit.MovementPoints -= cost;
                order.PathIndex++;

                // Update vision for the owning player
                updateVisionCallback?.Invoke(order.Unit);

                // Render to show the movement
                renderCallback?.Invoke();

                // Check for enemy units in vision range after moving
                if (HasEnemyInVision(order.Unit, owner))
                {
                    enemySpotted = true;
                    order.Unit.WakeUp(); // Wake up the unit
                    return false; // Cancel automatic movement
                }

                // Check if we've reached the destination
                if (order.Unit.Position.Equals(order.Destination))
                {
                    // Handle arrival at destination based on order type
                    if (order.OrderType == AutomaticOrderType.ReturnToBase)
                    {
                        HandleAircraftLanding(order.Unit);
                    }
                    return false; // Order complete
                }
            }
            else
            {
                // Not enough movement points
                break;
            }
        }


        // Return true if order should continue (not complete and no enemies spotted)
        return true;
    }

    private bool HasEnemyInVision(Unit unit, Player owner)
    {
        int visionRange = GetUnitVisionRange(unit);
        var tilesInVision = Map.GetTilesInRadius(unit.Position, visionRange);

        foreach (var tile in tilesInVision)
        {
            // Check if tile is visible and has line of sight
            if (owner.FogOfWar.ContainsKey(tile.Position) &&
                owner.FogOfWar[tile.Position] == VisibilityLevel.Visible &&
                Map.HasLineOfSight(unit.Position, tile.Position))
            {
                // Check for enemy units
                foreach (var otherUnit in tile.Units)
                {
                    if (otherUnit.OwnerId != owner.PlayerId)
                    {
                        return true;
                    }
                }

                // Check for enemy structures
                if (tile.Structure != null && tile.Structure.OwnerId != owner.PlayerId)
                {
                    return true;
                }
            }
        }

        return false;
    }
    private void HandleAircraftLanding(Unit unit)
    {
        if (!(unit is AirUnit airUnit))
            return;

        var tile = Map.GetTile(airUnit.Position);

        if (tile.Structure == null || tile.Structure.OwnerId != airUnit.OwnerId)
            return;

        // Try to land at base
        if (tile.Structure is Base baseStructure)
        {
            if (baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
            {
                tile.Units.Remove(airUnit);
                baseStructure.Airport.Add(airUnit);
                airUnit.HomeBaseId = baseStructure.StructureId;
                airUnit.Fuel = airUnit.MaxFuel; // Refuel
            }
        }
        else if (tile.Structure is City city)
        {
            if (city.Airport.Count < City.MAX_AIRPORT_CAPACITY)
            {
                tile.Units.Remove(airUnit);
                city.Airport.Add(airUnit);
                airUnit.HomeBaseId = city.StructureId;
                airUnit.Fuel = airUnit.MaxFuel; // Refuel
            }
        }
    }

    public void AddAutomaticOrder(Unit unit, TilePosition destination, AutomaticOrderType orderType)
    {
        // Remove any existing automatic orders for this unit
        var newQueue = new Queue<AutomaticOrder>();
        foreach (var order in AutomaticOrdersQueue)
        {
            if (order.Unit.UnitId != unit.UnitId)
            {
                newQueue.Enqueue(order);
            }
        }
        AutomaticOrdersQueue = newQueue;

        // Add the new order
        var newOrder = new AutomaticOrder(unit, destination, orderType);
        AutomaticOrdersQueue.Enqueue(newOrder);
    }
}