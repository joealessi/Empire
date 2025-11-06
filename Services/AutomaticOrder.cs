public enum AutomaticOrderType
{
    None,
    ReturnToBase,
    // Future: AutoPatrol, AutoExplore, etc.
}

public class AutomaticOrder
{
    public Unit Unit { get; set; }
    public TilePosition Destination { get; set; }
    public AutomaticOrderType OrderType { get; set; }
    public List<TilePosition> CurrentPath { get; set; }
    public int PathIndex { get; set; }
    
    public AutomaticOrder(Unit unit, TilePosition destination, AutomaticOrderType orderType)
    {
        Unit = unit;
        Destination = destination;
        OrderType = orderType;
        CurrentPath = new List<TilePosition>();
        PathIndex = 0;
    }
    
    public bool IsComplete()
    {
        return Unit.Position.Equals(Destination) || PathIndex >= CurrentPath.Count;
    }
}