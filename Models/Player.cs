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

    // Dynamic resource stockpile keyed by ResourceType (single source of truth).
    public Dictionary<ResourceType, int> Stockpile { get; } = new Dictionary<ResourceType, int>();

    public int GetResource(ResourceType type) => Stockpile.TryGetValue(type, out var v) ? v : 0;
    public void SetResource(ResourceType type, int value) => Stockpile[type] = value;
    public void AddResource(ResourceType type, int amount) => Stockpile[type] = GetResource(type) + amount;

    // Convenience facades over the stockpile so existing call sites keep working.
    public int Gold { get => GetResource(ResourceType.Gold); set => SetResource(ResourceType.Gold, value); }
    public int Steel { get => GetResource(ResourceType.Steel); set => SetResource(ResourceType.Steel, value); }
    public int Oil { get => GetResource(ResourceType.Oil); set => SetResource(ResourceType.Oil, value); }

    public bool HasBeenEliminated { get; set; }

    // Player-wide military upgrades (bought with populace at a city/base).
    public int ArmyHealthBonus { get; set; }
    public int TankHealthBonus { get; set; }
    public bool HasMilitary1 { get; set; }
    public bool HasMilitary2 { get; set; }
    public bool HasHighTechnology { get; set; }

    // Fractional resource accumulators — resources with fractional YieldPerTurn
    // (e.g. uranium at 0.25/turn) accumulate here until >= 1, then commit to the stockpile.
    public Dictionary<ResourceType, double> ResourceAccumulators { get; } = new Dictionary<ResourceType, double>();

    // AI behavior tracking
    public Dictionary<int, int> UnitsLostToPlayer { get; set; } = new Dictionary<int, int>();
    public List<Structure> LostStructures { get; set; } = new List<Structure>();
    public Dictionary<int, int> AggressionLevelTowardsPlayer { get; set; } = new Dictionary<int, int>();


    public HashSet<OrbitType> DeployedOrbitTypes { get; set; }

    // Commander-themed city naming
    public string CommanderName { get; set; }
    public int CityNameIndex { get; set; }

    public Player(int id, string name, bool isAI, int startingGold = 10, int startingSteel = 0, int startingOil = 0)
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

        // Seed every currency so the HUD/iteration always has an entry.
        foreach (var rt in ResourceRegistry.Currencies)
            Stockpile[rt] = 0;

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
                FogOfWar[pos] = VisibilityLevel.Explored;
        }

        // Update vision from all units
        foreach (var unit in Units)
        {
            if (unit is OrbitingSatellite || unit is GeosynchronousSatellite)
                UpdateSatelliteVision(map, unit.Position);
            else
                UpdateVisionFromPosition(map, unit.Position, GetUnitVisionRange(unit));
        }

        // Update vision from all structures
        foreach (var structure in Structures)
        {
            UpdateVisionFromPosition(map, structure.Position, structure.VisionRange);
        }
    }

    // Satellites reveal a 20-tile square centered on their position, ignoring LoS.
    private void UpdateSatelliteVision(Map map, TilePosition center)
    {
        const int half = 10;
        for (int dx = -half; dx <= half; dx++)
            for (int dy = -half; dy <= half; dy++)
            {
                var pos = new TilePosition(center.X + dx, center.Y + dy);
                if (map.IsValidPosition(pos))
                    FogOfWar[pos] = VisibilityLevel.Visible;
            }
    }

    private void UpdateVisionFromPosition(Map map, TilePosition position, int range)
    {
        var tiles = map.GetTilesInRadius(position, range);
        foreach (var tile in tiles)
        {
            // Mountains block line of sight - only reveal tiles the viewer can actually see
            if (!map.HasLineOfSight(position, tile.Position))
                continue;

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

    public void CalculateResourceIncome(Map map, double multiplier = 1.0)
    {
        var rates = GetResourceIncome(map);
        foreach (var kv in rates)
        {
            double earned = kv.Value * multiplier;
            if (earned == 0) continue;

            // Accumulate fractional amounts; commit whole units to the stockpile each turn.
            double acc = (ResourceAccumulators.TryGetValue(kv.Key, out var prev) ? prev : 0) + earned;
            int toAdd = (int)Math.Floor(acc);
            ResourceAccumulators[kv.Key] = acc - toAdd;
            if (toAdd > 0) AddResource(kv.Key, toAdd);
        }
    }

    // Income rates per resource this turn (may be fractional for resources like Uranium).
    // Gold comes from structures; mineable resources come from owned connected mines.
    public Dictionary<ResourceType, double> GetResourceIncome(Map map)
    {
        var income = new Dictionary<ResourceType, double>();
        foreach (var rt in ResourceRegistry.Currencies)
            income[rt] = 0;

        // Cities generate 3 gold, bases 1 gold (+Treasury upgrade bonus)
        foreach (var structure in Structures)
        {
            if (structure is City)
                income[ResourceType.Gold] += 3 + structure.GoldBonus;
            else if (structure is Base)
                income[ResourceType.Gold] += 1 + structure.GoldBonus;
        }

        // Mineable resources come from connected mines this player owns.
        foreach (var structure in Structures)
        {
            if (structure is Mine mine && mine.IsConnected)
                income[mine.Resource] += ResourceRegistry.Get(mine.Resource).YieldPerTurn;
        }

        return income;
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