public class Tile
{
    public TilePosition Position { get; set; }
    public TerrainType Terrain { get; set; }
    public int OwnerId { get; set; }
    public Structure Structure { get; set; }
    public List<Unit> Units { get; set; }
    
    // Movement cost by terrain type
    public int GetMovementCost(Unit unit)
    {
        // Air units ignore terrain
        if (unit is AirUnit)
            return 1;
        
        return Terrain switch
        {
            TerrainType.Ocean => 1,
            TerrainType.CoastalWater => 1,
            TerrainType.Land => 1,
            TerrainType.Plains => 1,
            TerrainType.Forest => 2,
            TerrainType.Hills => 2,
            TerrainType.Mountain => 3,
            _ => 1
        };
    }
    
    // Keep old property for compatibility but mark it
    public int MovementCost => GetMovementCost(null);
    
    // Defense bonus by terrain type (air units don't get terrain defense)
    public double GetDefenseBonus(Unit unit)
    {
        if (unit is AirUnit)
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
        OwnerId = -1; // Neutral
        Units = new List<Unit>();
    }
    
    public bool CanUnitEnter(Unit unit)
    {
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