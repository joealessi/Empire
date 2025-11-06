public enum AutomaticOrderType
{
    None,
    ReturnToBase,
    Patrol
}

public class AutomaticOrder
{
    public Unit Unit { get; set; }
    public TilePosition Destination { get; set; }
    public AutomaticOrderType OrderType { get; set; }
    public List<TilePosition> CurrentPath { get; set; }
    public int PathIndex { get; set; }
    
    // For patrol orders
    public List<TilePosition> PatrolWaypoints { get; set; }
    public int CurrentWaypointIndex { get; set; }
    
    public AutomaticOrder(Unit unit, TilePosition destination, AutomaticOrderType orderType)
    {
        Unit = unit;
        Destination = destination;
        OrderType = orderType;
        CurrentPath = new List<TilePosition>();
        PathIndex = 0;
        PatrolWaypoints = new List<TilePosition>();
        CurrentWaypointIndex = 0;
    }
    
    public bool IsComplete()
    {
        // Patrol orders never complete, they cycle
        if (OrderType == AutomaticOrderType.Patrol)
            return false;
            
        return Unit.Position.Equals(Destination) || PathIndex >= CurrentPath.Count;
    }
}