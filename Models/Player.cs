// Structures (non-mobile)


// Tile

// Map

// Helper class for pathfinding

// Player
public class Player
{
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public bool IsAI { get; set; }
    public List<Unit> Units { get; set; }
    public List<Structure> Structures { get; set; }
    public Dictionary<TilePosition, VisibilityLevel> FogOfWar { get; set; }
    
    public Player(int id, string name, bool isAI)
    {
        PlayerId = id;
        Name = name;
        IsAI = isAI;
        Units = new List<Unit>();
        Structures = new List<Structure>();
        FogOfWar = new Dictionary<TilePosition, VisibilityLevel>();
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
            _ => 2
        };
    }}

// Game