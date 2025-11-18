public enum ResourceType
{
    None,
    Oil,
    Steel
}

public class Tile
{
    public TilePosition Position { get; set; }
    public TerrainType Terrain { get; set; }
    public ResourceType Resource { get; set; }
    public int OwnerId { get; set; }
    public Structure Structure { get; set; }
    public List<Unit> Units { get; set; }
    public bool HasBridge { get; set; }
    public string BridgeName { get; set; }
    
    // Movement cost by terrain type
    public double GetMovementCost(Unit unit)
    {
        // Bridges make water crossable for land units
        if (HasBridge && unit is LandUnit)
        {
            return 1.0; // Normal movement cost across bridge
        }

        // Check if unit can traverse this terrain
        if (unit is LandUnit)
        {
            if (Terrain == TerrainType.Ocean || Terrain == TerrainType.CoastalWater)
                return double.MaxValue; // Can't cross water without bridge
        }
        else if (unit is SeaUnit)
        {
            if (Terrain != TerrainType.Ocean && Terrain != TerrainType.CoastalWater)
                return double.MaxValue; // Can't move on land
        }

        // Return terrain-based cost
        return Terrain switch
        {
            TerrainType.Plains => 1.0,
            TerrainType.Land => 1.0,
            TerrainType.Forest => 1.5,
            TerrainType.Hills => 2.0,
            TerrainType.Mountain => 3.0,
            TerrainType.Ocean => 1.0,
            TerrainType.CoastalWater => 1.0,
            _ => 1.0
        };
    }
    
    // Keep old property for compatibility but mark it
    public int MovementCost => (int)GetMovementCost(null);
    
    // Defense bonus by terrain type (air units and satellites don't get terrain defense)
    public double GetDefenseBonus(Unit unit)
    {
        if (unit is AirUnit || unit is Satellite)
            return 1.0;
        
        return Terrain switch
        {
            TerrainType.Forest => 1.2,
            TerrainType.Hills => 1.3,
            TerrainType.Mountain => 1.5,
            _ => 1.0
        };
    }
    
    // Keep old property for compatibility
    public double DefenseBonus => GetDefenseBonus(null);
    
    public Tile(TilePosition position, TerrainType terrain)
    {
        Position = position;
        Terrain = terrain;
        Resource = ResourceType.None;  
        OwnerId = -1;
        Units = new List<Unit>();
    }
    
    public bool CanUnitEnter(Unit unit)
    {
        // Satellites can go anywhere (in orbit above terrain)
        if (unit is Satellite)
        {
            return true;
        }
        
        if (unit is LandUnit)
        {
            return Terrain != TerrainType.Ocean && Terrain != TerrainType.CoastalWater;
        }
        else if (unit is SeaUnit)
        {
            return Terrain == TerrainType.Ocean || Terrain == TerrainType.CoastalWater;
        }
        else if (unit is AirUnit)
        {
            return true; // Air units can fly over anything
        }
        
        return false;
    }
    
    public bool IsCoastalWater(Map map)
    {
        if (Terrain != TerrainType.Ocean && Terrain != TerrainType.CoastalWater)
            return false;
        
        // Check if adjacent to land
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                
                var neighborPos = new TilePosition(Position.X + dx, Position.Y + dy);
                if (map.IsValidPosition(neighborPos))
                {
                    var neighbor = map.GetTile(neighborPos);
                    if (neighbor.Terrain != TerrainType.Ocean && 
                        neighbor.Terrain != TerrainType.CoastalWater)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}