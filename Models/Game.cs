using static EmpireGame.MainWindow;

public class Game
{
    public Map Map { get; set; }
    public List<Player> Players { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public int TurnNumber { get; set; }

    private int nextUnitId = 1;
    private int nextStructureId = 1;
    public Queue<AutomaticOrder> AutomaticOrdersQueue { get; set; }
    public Queue<string> ProductionMessages { get; set; }

    public bool HasSurrendered { get; set; }

    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public Game(int mapWidth, int mapHeight, int playerCount, int startingGold = 10, int startingSteel = 0, int startingOil = 1)
    {
        Map = new Map(mapWidth, mapHeight);
        Players = new List<Player>();

        for (int i = 0; i < playerCount; i++)
        {
            Players.Add(new Player(i, $"Player {i + 1}", i > 0, startingGold, startingSteel, startingOil));
        }

        CurrentPlayerIndex = 0;
        TurnNumber = 1;

        AutomaticOrdersQueue = new Queue<AutomaticOrder>();
        ProductionMessages = new Queue<string>();    }

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

            // Process units
            foreach (var unit in player.Units.ToList())
            {
                if (unit is AirUnit airUnit)
                {
                    airUnit.ConsumeFuel();
                }
                else if (unit is Satellite satellite)
                {
                    // Age satellites
                    satellite.AgeSatellite();

                    // If satellite died, unregister its orbit type
                    if (satellite.Life <= 0 && satellite is OrbitingSatellite orbitSat)
                    {
                        player.UnregisterOrbitingSatellite(orbitSat.Orbit);
                    }

                    // Move orbiting satellites
                    if (unit is OrbitingSatellite orbitingSat && orbitingSat.Life > 0)
                    {
                        MoveOrbitingSatellite(orbitingSat, player);
                    }
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

    private void MoveOrbitingSatellite(OrbitingSatellite satellite, Player player)
    {
        var currentTile = Map.GetTile(satellite.Position);
        currentTile.Units.Remove(satellite);
        
        var nextPosition = satellite.GetNextOrbitPosition(Map);
        satellite.Position = nextPosition;
        
        var nextTile = Map.GetTile(nextPosition);
        nextTile.Units.Add(satellite);
        
        // Update vision for the satellite's new position
        player.UpdateVision(Map);
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
            OrbitingSatellite sat => sat.VisionRadius,
            GeosynchronousSatellite geosat => geosat.VisionRadius,
            _ => 2
        };
    }

    private void ProcessUnitOrders(Unit unit)
    {
        if (unit.CurrentOrders.Type == OrderType.Patrol &&
            unit.CurrentOrders.PatrolWaypoints.Count > 0)
        {
            ProcessPatrolOrder(unit);
        }
        else if (unit is Bomber bomber &&
                 unit.CurrentOrders.Type == OrderType.BombingRun)
        {
            // Process bomber movement along flight path
        }
    }

    private void ProcessPatrolOrder(Unit unit)
    {
        var waypoints = unit.CurrentOrders.PatrolWaypoints;

        // For aircraft at base, take off and start patrol
        if (unit is AirUnit airUnit && airUnit.HomeBaseId != -1)
        {
            Structure homeBase = null;
            foreach (var structure in Players[unit.OwnerId].Structures)
            {
                if (structure.StructureId == airUnit.HomeBaseId)
                {
                    homeBase = structure;
                    break;
                }
            }

            if (homeBase != null)
            {
                if (homeBase is Base baseStructure)
                {
                    baseStructure.Airport.Remove(airUnit);
                }
                else if (homeBase is City city)
                {
                    city.Airport.Remove(airUnit);
                }

                var adjacentPos = FindAdjacentEmptyTile(homeBase.Position);
                if (adjacentPos.X != -1)
                {
                    airUnit.Position = adjacentPos;
                    airUnit.HomeBaseId = -1;
                    airUnit.Fuel = airUnit.MaxFuel;

                    var tile = Map.GetTile(adjacentPos);
                    tile.Units.Add(airUnit);

                    return;
                }
            }
        }

        // Check if unit is at base position and should land
        if (unit is AirUnit patrolAircraft && patrolAircraft.HomeBaseId == -1)
        {
            // Check if current position is a base
            var currentTile = Map.GetTile(unit.Position);
            if (currentTile.Structure != null &&
                currentTile.Structure.OwnerId == unit.OwnerId &&
                (currentTile.Structure is Base || currentTile.Structure is City))
            {
                // We're at a base - check if this is the patrol start base
                if (currentTile.Structure.Position.Equals(waypoints[0]))
                {
                    // Land and refuel
                    if (currentTile.Structure is Base baseStructure &&
                        baseStructure.Airport.Count < Base.MAX_AIRPORT_CAPACITY)
                    {
                        currentTile.Units.Remove(patrolAircraft);

                        baseStructure.Airport.Add(patrolAircraft);
                        patrolAircraft.HomeBaseId = baseStructure.StructureId;
                        patrolAircraft.Fuel = patrolAircraft.MaxFuel;

                        // Next turn will take off and restart patrol
                        return;
                    }
                    else if (currentTile.Structure is City cityStructure &&
                             cityStructure.Airport.Count < City.MAX_AIRPORT_CAPACITY)
                    {
                        currentTile.Units.Remove(patrolAircraft);

                        cityStructure.Airport.Add(patrolAircraft);
                        patrolAircraft.HomeBaseId = cityStructure.StructureId;
                        patrolAircraft.Fuel = patrolAircraft.MaxFuel;

                        return;
                    }
                }
            }
        }

        // Find current position in patrol route
        int currentIndex = -1;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < waypoints.Count; i++)
        {
            int distance = Math.Abs(unit.Position.X - waypoints[i].X) +
                          Math.Abs(unit.Position.Y - waypoints[i].Y);

            if (distance == 0)
            {
                currentIndex = i;
                break;
            }
            else if (distance < closestDistance)
            {
                closestDistance = distance;
                currentIndex = i;
            }
        }

        // Determine next waypoint in sequence
        int nextIndex = currentIndex + 1;

        // If at the end of the patrol route, loop back to start
        if (nextIndex >= waypoints.Count)
        {
            nextIndex = 0;
        }

        TilePosition nextWaypoint = waypoints[nextIndex];

        // Check for enemies in vision range before moving
        if (HasEnemyInVision(unit, Players[unit.OwnerId]))
        {
            unit.WakeUp();
            unit.CurrentOrders.Type = OrderType.None;
            unit.CurrentOrders.PatrolWaypoints.Clear();
            return;
        }

        // Calculate path to next waypoint
        var path = Map.FindPath(unit.Position, nextWaypoint, unit);

        if (path.Count > 1)
        {
            double movementLeft = unit.MovementPoints;
            int stepIndex = 1;

            while (stepIndex < path.Count && movementLeft >= 0.5)
            {
                var nextTile = Map.GetTile(path[stepIndex]);
                double cost = nextTile.GetMovementCost(unit);

                if (cost <= movementLeft && nextTile.CanUnitEnter(unit))
                {
                    var oldTile = Map.GetTile(unit.Position);
                    oldTile.Units.Remove(unit);

                    unit.Position = path[stepIndex];
                    movementLeft -= cost;

                    nextTile.Units.Add(unit);

                    // After moving, check if we landed on base
                    if (unit is AirUnit movedAircraft && movedAircraft.HomeBaseId == -1)
                    {
                        var movedTile = Map.GetTile(unit.Position);
                        if (movedTile.Structure != null &&
                            movedTile.Structure.OwnerId == unit.OwnerId &&
                            movedTile.Structure.Position.Equals(waypoints[0]))
                        {
                            // Landed on base - will land next iteration
                            unit.MovementPoints = movementLeft;
                            return;
                        }
                    }

                    // Check for enemies after moving
                    if (HasEnemyInVision(unit, Players[unit.OwnerId]))
                    {
                        unit.WakeUp();
                        unit.CurrentOrders.Type = OrderType.None;
                        unit.CurrentOrders.PatrolWaypoints.Clear();
                        break;
                    }

                    stepIndex++;
                }
                else
                {
                    break;
                }
            }

            unit.MovementPoints = movementLeft;
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
            
            // Handle satellite orbit type registration and message
            if (currentOrder is SatelliteProductionOrder satOrder && unit is OrbitingSatellite orbitSat)
            {
                orbitSat.Orbit = satOrder.OrbitType;
                player.RegisterOrbitingSatellite(satOrder.OrbitType);
                
                // Add launch message
                string orbitName = satOrder.OrbitType switch
                {
                    OrbitType.Horizontal => "Horizontal",
                    OrbitType.Vertical => "Vertical", 
                    OrbitType.RightDiagonal => "Right Diagonal",
                    OrbitType.LeftDiagonal => "Left Diagonal",
                    _ => "Unknown"
                };
                ProductionMessages.Enqueue($"🛰️ Orbiting Satellite launched! ({orbitName} orbit)");
            }
            else if (unit is GeosynchronousSatellite)
            {
                ProductionMessages.Enqueue($"🛰️ Geosynchronous Satellite launched!");
            }

            // Place unit in appropriate storage (check capacity)
            bool placed = false;
            
            // Satellites go directly to the map (not in storage)
            if (unit is Satellite satellite)
            {
                var adjacentPos = FindAdjacentEmptyTile(baseStructure.Position);
                if (adjacentPos.X != -1)
                {
                    satellite.Position = adjacentPos;
                    var tile = Map.GetTile(adjacentPos);
                    tile.Units.Add(satellite);
                    placed = true;
                }
            }
            else if (unit is AirUnit airUnit)
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
            
            // Handle satellite orbit type registration and message
            if (currentOrder is SatelliteProductionOrder satOrder && unit is OrbitingSatellite orbitSat)
            {
                orbitSat.Orbit = satOrder.OrbitType;
                player.RegisterOrbitingSatellite(satOrder.OrbitType);
                
                // Add launch message
                string orbitName = satOrder.OrbitType switch
                {
                    OrbitType.Horizontal => "Horizontal",
                    OrbitType.Vertical => "Vertical",
                    OrbitType.RightDiagonal => "Right Diagonal",
                    OrbitType.LeftDiagonal => "Left Diagonal",
                    _ => "Unknown"
                };
                ProductionMessages.Enqueue($"🛰️ Orbiting Satellite launched! ({orbitName} orbit)");
            }
            else if (unit is GeosynchronousSatellite)
            {
                ProductionMessages.Enqueue($"🛰️ Geosynchronous Satellite launched!");
            }

            // Place unit in appropriate storage (check capacity)
            bool placed = false;
            
            // Satellites go directly to the map (not in storage)
            if (unit is Satellite satellite)
            {
                var adjacentPos = FindAdjacentEmptyTile(city.Position);
                if (adjacentPos.X != -1)
                {
                    satellite.Position = adjacentPos;
                    var tile = Map.GetTile(adjacentPos);
                    tile.Units.Add(satellite);
                    placed = true;
                }
            }
            else if (unit is AirUnit airUnit)
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
    public async Task<(bool shouldContinue, bool enemySpotted)> ProcessAutomaticOrder(AutomaticOrder order, Action<Unit> updateVisionCallback, Func<Task> renderCallback)
    {
        bool enemySpotted = false;

        if (order.Unit.Life <= 0)
        {
            return (false, false);
        }

        var owner = Players.FirstOrDefault(p => p.PlayerId == order.Unit.OwnerId);
        if (owner == null)
            return (false, false);

        // Handle patrol orders differently
        if (order.OrderType == AutomaticOrderType.Patrol)
        {
            return await ProcessPatrolOrder(order, owner, updateVisionCallback, renderCallback);
        }

        // Regular movement orders (ReturnToBase, etc.)
        if (order.CurrentPath.Count == 0 || order.PathIndex >= order.CurrentPath.Count)
        {
            order.CurrentPath = Map.FindPath(order.Unit.Position, order.Destination, order.Unit);
            order.PathIndex = 1;

            if (order.CurrentPath.Count == 0)
            {
                return (false, false);
            }
        }

        while (order.PathIndex < order.CurrentPath.Count && order.Unit.MovementPoints >= 0.5)
        {
            var nextTile = Map.GetTile(order.CurrentPath[order.PathIndex]);
            double cost = nextTile.GetMovementCost(order.Unit);

            if (cost <= order.Unit.MovementPoints)
            {
                bool isDestination = order.CurrentPath[order.PathIndex].Equals(order.Destination);
                bool occupied = nextTile.Units.Any(u => u.OwnerId == order.Unit.OwnerId && u.UnitId != order.Unit.UnitId);

                if (occupied && !isDestination)
                {
                    order.PathIndex = order.CurrentPath.Count;
                    break;
                }

                // In ProcessAutomaticOrder, after moving the unit:
                var oldTile = Map.GetTile(order.Unit.Position);
                oldTile.Units.Remove(order.Unit);

                order.Unit.Position = order.CurrentPath[order.PathIndex];
                nextTile.Units.Add(order.Unit);

                // NEW: Claim tile ownership
                nextTile.OwnerId = order.Unit.OwnerId;

                order.Unit.MovementPoints -= cost;
                order.PathIndex++;

                updateVisionCallback?.Invoke(order.Unit);

                if (renderCallback != null)
                {
                    await renderCallback();
                }

                if (HasEnemyInVision(order.Unit, owner))
                {
                    enemySpotted = true;
                    order.Unit.WakeUp();
                    return (false, enemySpotted);
                }

                if (order.Unit.Position.Equals(order.Destination))
                {
                    if (order.OrderType == AutomaticOrderType.ReturnToBase)
                    {
                        HandleAircraftLanding(order.Unit);
                    }
                    return (false, enemySpotted);
                }
            }
            else
            {
                break;
            }
        }

        return (true, enemySpotted);
    }

    private async Task<(bool shouldContinue, bool enemySpotted)> ProcessPatrolOrder(AutomaticOrder order, Player owner, Action<Unit> updateVisionCallback, Func<Task> renderCallback)
    {
        bool enemySpotted = false;

        if (order.PatrolWaypoints.Count == 0)
            return (false, false);

        // Main movement loop - continue until out of movement points
        while (order.Unit.MovementPoints >= 0.5)
        {
            // Recalculate path if needed
            if (order.CurrentPath.Count == 0 || order.PathIndex >= order.CurrentPath.Count)
            {
                TilePosition nextWaypoint = order.PatrolWaypoints[order.CurrentWaypointIndex];

                order.CurrentPath = Map.FindPath(order.Unit.Position, nextWaypoint, order.Unit);
                order.PathIndex = 1;

                if (order.CurrentPath.Count == 0)
                {
                    return (false, false);
                }
            }

            // Move along current path
            bool movedThisTurn = false;
            while (order.PathIndex < order.CurrentPath.Count && order.Unit.MovementPoints >= 0.5)
            {
                var nextTile = Map.GetTile(order.CurrentPath[order.PathIndex]);
                double cost = nextTile.GetMovementCost(order.Unit);

                if (cost <= order.Unit.MovementPoints)
                {
                    bool occupied = nextTile.Units.Any(u => u.OwnerId == order.Unit.OwnerId && u.UnitId != order.Unit.UnitId);

                    if (occupied)
                    {
                        order.PathIndex = order.CurrentPath.Count;
                        return (true, false);
                    }

                    // In ProcessAutomaticOrder, after moving the unit:
                    var oldTile = Map.GetTile(order.Unit.Position);
                    oldTile.Units.Remove(order.Unit);

                    order.Unit.Position = order.CurrentPath[order.PathIndex];
                    nextTile.Units.Add(order.Unit);

                    // NEW: Claim tile ownership
                    nextTile.OwnerId = order.Unit.OwnerId;

                    order.Unit.MovementPoints -= cost;
                    order.PathIndex++;
                    movedThisTurn = true;

                    updateVisionCallback?.Invoke(order.Unit);

                    if (renderCallback != null)
                    {
                        await renderCallback();
                    }

                    if (HasEnemyInVision(order.Unit, owner))
                    {
                        enemySpotted = true;
                        order.Unit.WakeUp();
                        return (false, enemySpotted);
                    }

                    // Check if we've reached the current waypoint
                    TilePosition currentWaypoint = order.PatrolWaypoints[order.CurrentWaypointIndex];
                    if (order.Unit.Position.Equals(currentWaypoint))
                    {
                        // SPECIAL HANDLING FOR AIRCRAFT AT BASE
                        if (order.Unit is AirUnit airUnit)
                        {
                            var tile = Map.GetTile(airUnit.Position);

                            // Check if this waypoint is a base/city
                            if (tile.Structure != null &&
                                tile.Structure.OwnerId == airUnit.OwnerId &&
                                (tile.Structure is Base || tile.Structure is City))
                            {
                                // Land, refuel, and take off
                                bool landedSuccessfully = HandleAircraftPatrolRefuel(airUnit, tile.Structure);

                                if (!landedSuccessfully)
                                {
                                    // Airport full, can't continue patrol
                                    return (false, false);
                                }

                                // Aircraft has been refueled and is back on the map
                                // Update the position since it was moved during land/takeoff
                            }
                        }

                        // Move to next waypoint
                        order.CurrentWaypointIndex++;
                        if (order.CurrentWaypointIndex >= order.PatrolWaypoints.Count)
                        {
                            order.CurrentWaypointIndex = 1;
                        }

                        order.CurrentPath.Clear();
                        order.PathIndex = 0;
                        break;
                    }
                }
                else
                {
                    return (true, false);
                }
            }

            if (!movedThisTurn)
            {
                break;
            }
        }

        return (true, enemySpotted);
    }

    private bool HandleAircraftPatrolRefuel(AirUnit airUnit, Structure structure)
    {
        // Remove from current tile
        var currentTile = Map.GetTile(airUnit.Position);
        currentTile.Units.Remove(airUnit);

        // Land at the structure
        if (structure is Base baseStructure)
        {
            if (baseStructure.Airport.Count >= Base.MAX_AIRPORT_CAPACITY)
            {
                // Airport full - put unit back and cancel patrol
                currentTile.Units.Add(airUnit);
                return false;
            }

            baseStructure.Airport.Add(airUnit);
            airUnit.HomeBaseId = baseStructure.StructureId;
            airUnit.Fuel = airUnit.MaxFuel; // Refuel

            // Immediately take off again
            var adjacentPos = FindAdjacentEmptyTileForTakeoff(baseStructure.Position);
            if (adjacentPos.X != -1)
            {
                baseStructure.Airport.Remove(airUnit);
                airUnit.Position = adjacentPos;
                airUnit.HomeBaseId = -1;

                var newTile = Map.GetTile(adjacentPos);
                newTile.Units.Add(airUnit);

                return true;
            }
            else
            {
                // No space to take off - remove from patrol
                return false;
            }
        }
        else if (structure is City city)
        {
            if (city.Airport.Count >= City.MAX_AIRPORT_CAPACITY)
            {
                currentTile.Units.Add(airUnit);
                return false;
            }

            city.Airport.Add(airUnit);
            airUnit.HomeBaseId = city.StructureId;
            airUnit.Fuel = airUnit.MaxFuel; // Refuel

            var adjacentPos = FindAdjacentEmptyTileForTakeoff(city.Position);
            if (adjacentPos.X != -1)
            {
                city.Airport.Remove(airUnit);
                airUnit.Position = adjacentPos;
                airUnit.HomeBaseId = -1;

                var newTile = Map.GetTile(adjacentPos);
                newTile.Units.Add(airUnit);

                return true;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    private TilePosition FindAdjacentEmptyTileForTakeoff(TilePosition centerPos)
    {
        int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };

        for (int i = 0; i < 8; i++)
        {
            var pos = new TilePosition(centerPos.X + dx[i], centerPos.Y + dy[i]);

            if (Map.IsValidPosition(pos))
            {
                var tile = Map.GetTile(pos);

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

        return new TilePosition(-1, -1);
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