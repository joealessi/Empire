public abstract class AirUnit : Unit
{
    public int Fuel { get; set; }
    public int MaxFuel { get; set; }
    public int HomeBaseId { get; set; }
    
    public bool IsInAir => HomeBaseId == -1;
    
    public virtual void ConsumeFuel()
    {
        if (IsInAir)
        {
            Fuel--;
            if (Fuel <= 0)
            {
                Crash();
            }
        }
    }
    
    protected virtual void Crash()
    {
        Life = 0;
    }
    
    // CORRECTED: Use actual pathfinding to calculate fuel distance
    // This accounts for diagonal movement and actual paths
    public int GetDistanceToNearestBase(Map map, Player player)
    {
        int minDistance = int.MaxValue;
        
        // Check all friendly structures
        foreach (var structure in player.Structures)
        {
            if (structure is Base || structure is City)
            {
                // Calculate actual path distance using pathfinding
                var path = map.FindPath(Position, structure.Position, this);
                if (path.Count > 0)
                {
                    // Path includes starting position, so distance is count - 1
                    int distance = path.Count - 1;
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
            }
        }
        
        // Check for friendly carriers
        foreach (var unit in player.Units)
        {
            if (unit is Carrier carrier && carrier.CanDock(this))
            {
                var path = map.FindPath(Position, unit.Position, this);
                if (path.Count > 0)
                {
                    int distance = path.Count - 1;
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
            }
        }
        
        return minDistance == int.MaxValue ? -1 : minDistance;
    }
    
    public Structure GetNearestBase(Map map, Player player)
    {
        Structure nearestBase = null;
        int minDistance = int.MaxValue;
        
        foreach (var structure in player.Structures)
        {
            if (structure is Base || structure is City)
            {
                // Use actual pathfinding for accurate distance
                var path = map.FindPath(Position, structure.Position, this);
                if (path.Count > 0)
                {
                    int distance = path.Count - 1;
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestBase = structure;
                    }
                }
            }
        }
        
        return nearestBase;
    }
}