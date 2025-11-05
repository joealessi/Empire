public class Player
{
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public bool IsAI { get; set; }
    public List<Unit> Units { get; set; }
    public List<Structure> Structures { get; set; }
    public Dictionary<TilePosition, VisibilityLevel> FogOfWar { get; set; }
    
    public int Gold { get; set; }
    public int Steel { get; set; }
    public int Oil { get; set; }
    
    public Player(int id, string name, bool isAI)
    {
        PlayerId = id;
        Name = name;
        IsAI = isAI;
        Units = new List<Unit>();
        Structures = new List<Structure>();
        FogOfWar = new Dictionary<TilePosition, VisibilityLevel>();
        
        // Starting resources
        Gold = 10;
        Steel = 2;
        Oil = 2;
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
            
            // Structures control 8 adjacent tiles
            goldIncome += CountAdjacentResourceIncome(map, structure.Position, ResourceType.None);
            steelIncome += CountAdjacentResourceIncome(map, structure.Position, ResourceType.Steel);
            oilIncome += CountAdjacentResourceIncome(map, structure.Position, ResourceType.Oil);
        }
        
        // Units standing on resource tiles
        foreach (var unit in Units)
        {
            var tile = map.GetTile(unit.Position);
            if (tile != null)
            {
                if (tile.Resource == ResourceType.Steel)
                    steelIncome += 1;
                else if (tile.Resource == ResourceType.Oil)
                    oilIncome += 1;
            }
        }
        
        // Apply income
        Gold += goldIncome;
        Steel += steelIncome;
        Oil += oilIncome;
    }
    
    private int CountAdjacentResourceIncome(Map map, TilePosition center, ResourceType resourceType)
    {
        int count = 0;
        int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };
        
        for (int i = 0; i < 8; i++)
        {
            var pos = new TilePosition(center.X + dx[i], center.Y + dy[i]);
            if (map.IsValidPosition(pos))
            {
                var tile = map.GetTile(pos);
                if (tile.Resource == resourceType)
                    count += 1;
            }
        }
        
        return count;
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
            
            // Structures control 8 adjacent tiles
            steelIncome += CountAdjacentResourceIncome(map, structure.Position, ResourceType.Steel);
            oilIncome += CountAdjacentResourceIncome(map, structure.Position, ResourceType.Oil);
        }
        
        // Units standing on resource tiles
        foreach (var unit in Units)
        {
            var tile = map.GetTile(unit.Position);
            if (tile != null)
            {
                if (tile.Resource == ResourceType.Steel)
                    steelIncome += 1;
                else if (tile.Resource == ResourceType.Oil)
                    oilIncome += 1;
            }
        }
        
        return (goldIncome, steelIncome, oilIncome);
    }
}
