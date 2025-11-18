using EmpireGame;

public class Player
{
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public bool IsAI { get; set; }
    public List<Unit> Units { get; set; }
    public List<Structure> Structures { get; set; }
    public Dictionary<TilePosition, VisibilityLevel> FogOfWar { get; set; }
    public AIPersonality Personality { get; set; }
    public GameStatistics Statistics { get; set; }

    public int Gold { get; set; }
    public int Steel { get; set; }
    public int Oil { get; set; }

    public bool HasBeenEliminated { get; set; }

    // AI behavior tracking
    public Dictionary<int, int> UnitsLostToPlayer { get; set; } = new Dictionary<int, int>();
    public List<Structure> LostStructures { get; set; } = new List<Structure>();
    public Dictionary<int, int> AggressionLevelTowardsPlayer { get; set; } = new Dictionary<int, int>();


    public HashSet<OrbitType> DeployedOrbitTypes { get; set; }

    public Player(int id, string name, bool isAI, int startingGold = 10, int startingSteel = 0, int startingOil = 1)
    {
        PlayerId = id;
        Name = name;
        IsAI = isAI;
        Units = new List<Unit>();
        Structures = new List<Structure>();
        FogOfWar = new Dictionary<TilePosition, VisibilityLevel>();
        DeployedOrbitTypes = new HashSet<OrbitType>();
        UnitsLostToPlayer = new Dictionary<int, int>();
        LostStructures = new List<Structure>();
        AggressionLevelTowardsPlayer = new Dictionary<int, int>();
        HasBeenEliminated = false;
        Statistics = new GameStatistics();
        Statistics.PlayerName = name;

        // Starting resources from parameters
        Gold = startingGold;
        Steel = startingSteel;
        Oil = startingOil;
    }

    public void UpdateStatistics(Map map)
    {
        int currentBases = Structures.Count(s => s is Base);
        int currentCities = Structures.Count(s => s is City);
    
        Statistics.MaxBasesOwned = Math.Max(Statistics.MaxBasesOwned, currentBases);
        Statistics.MaxCitiesOwned = Math.Max(Statistics.MaxCitiesOwned, currentCities);
    }

    public void UpdateVision(Map map)
    {
        // Reset all tiles to explored (if previously visible)
        var keysToUpdate = FogOfWar.Keys.ToList();
        foreach (var pos in keysToUpdate)
        {
            if (FogOfWar[pos] == VisibilityLevel.Visible)
            {
                FogOfWar[pos] = VisibilityLevel.Explored;
            }
        }

        // Update vision from all units
        foreach (var unit in Units)
        {
            UpdateVisionFromPosition(map, unit.Position, GetUnitVisionRange(unit));
        }

        // Update vision from all structures
        foreach (var structure in Structures)
        {
            UpdateVisionFromPosition(map, structure.Position, structure.VisionRange);
        }
    }

    private void UpdateVisionFromPosition(Map map, TilePosition position, int range)
    {
        var tiles = map.GetTilesInRadius(position, range);
        foreach (var tile in tiles)
        {
            FogOfWar[tile.Position] = VisibilityLevel.Visible;
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

    public void CalculateResourceIncome(Map map)
    {
        int goldIncome = 0;
        int steelIncome = 0;
        int oilIncome = 0;

        // Cities generate 3 gold, bases generate 1 gold
        foreach (var structure in Structures)
        {
            if (structure is City)
                goldIncome += 3;
            else if (structure is Base)
                goldIncome += 1;
        }

        // Count ALL tiles owned by this player (NOT just where units are standing)
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var pos = new TilePosition(x, y);
                var tile = map.GetTile(pos);

                // Only count tiles owned by this player
                if (tile.OwnerId == PlayerId)
                {
                    if (tile.Resource == ResourceType.Steel)
                        steelIncome += 1;
                    else if (tile.Resource == ResourceType.Oil)
                        oilIncome += 1;
                }
            }
        }

        // Apply income
        Gold += goldIncome;
        Steel += steelIncome;
        Oil += oilIncome;
    }

    public (int goldIncome, int steelIncome, int oilIncome) GetResourceIncome(Map map)
    {
        int goldIncome = 0;
        int steelIncome = 0;
        int oilIncome = 0;

        // Cities generate 3 gold, bases generate 1 gold
        foreach (var structure in Structures)
        {
            if (structure is City)
                goldIncome += 3;
            else if (structure is Base)
                goldIncome += 1;
        }

        // Count ALL tiles owned by this player (NOT just where units are standing)
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var pos = new TilePosition(x, y);
                var tile = map.GetTile(pos);

                // Only count tiles owned by this player
                if (tile.OwnerId == PlayerId)
                {
                    if (tile.Resource == ResourceType.Steel)
                        steelIncome += 1;
                    else if (tile.Resource == ResourceType.Oil)
                        oilIncome += 1;
                }
            }
        }

        return (goldIncome, steelIncome, oilIncome);
    }

    public bool CanDeployOrbitingSatellite(OrbitType orbitType)
    {
        return !DeployedOrbitTypes.Contains(orbitType);
    }

    public void RegisterOrbitingSatellite(OrbitType orbitType)
    {
        DeployedOrbitTypes.Add(orbitType);
    }

    public void UnregisterOrbitingSatellite(OrbitType orbitType)
    {
        DeployedOrbitTypes.Remove(orbitType);
    }

    public void RecordUnitLoss(int killerPlayerId)
    {
        if (!UnitsLostToPlayer.ContainsKey(killerPlayerId))
        {
            UnitsLostToPlayer[killerPlayerId] = 0;
        }
        UnitsLostToPlayer[killerPlayerId]++;
    
        // Track statistics
        Statistics.UnitsLost++;
    
        // Update aggression based on personality
        if (Personality != null && IsAI)
        {
            if (!AggressionLevelTowardsPlayer.ContainsKey(killerPlayerId))
            {
                AggressionLevelTowardsPlayer[killerPlayerId] = 0;
            }
        
            if (Personality.Playstyle == AIPlaystyle.Aggressive && UnitsLostToPlayer[killerPlayerId] >= 1)
            {
                AggressionLevelTowardsPlayer[killerPlayerId] = 2;
            }
            else if (Personality.Playstyle == AIPlaystyle.Balanced && UnitsLostToPlayer[killerPlayerId] >= 2)
            {
                AggressionLevelTowardsPlayer[killerPlayerId] = 1;
            }
        }
    }

    public void RecordStructureLoss(Structure structure)
    {
        LostStructures.Add(structure);
        Statistics.StructuresLost++;
    }

    public void RecordEnemyKill()
    {
        Statistics.EnemyUnitsDestroyed++;
    }

    public void RecordStructureCapture()
    {
        Statistics.StructuresCaptured++;
    }

    public int GetAggressionTowardsPlayer(int targetPlayerId)
    {
        if (AggressionLevelTowardsPlayer.ContainsKey(targetPlayerId))
        {
            return AggressionLevelTowardsPlayer[targetPlayerId];
        }
        return 0;
    }
}