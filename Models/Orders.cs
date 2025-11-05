public class Orders
{
    public OrderType Type { get; set; }
    public TilePosition TargetPosition { get; set; }  // Changed from Point
    public List<TilePosition> PatrolWaypoints { get; set; }  // Changed from List<Point>
    public Unit TargetUnit { get; set; }
    
    public Orders()
    {
        PatrolWaypoints = new List<TilePosition>();
    }
}