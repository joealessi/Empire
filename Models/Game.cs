public class Game
{
    public Map Map { get; set; }
    public List<Player> Players { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public int TurnNumber { get; set; }

    private int nextUnitId = 1;
    private int nextStructureId = 1;

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

                // Process automated orders (patrol, bombing runs, etc.)
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
            // Production complete, create unit
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
            // Production complete, create unit
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
}