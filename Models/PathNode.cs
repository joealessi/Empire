public class PathNode
{
    public TilePosition Position { get; set; }
    public int FScore { get; set; }
    
    public PathNode(TilePosition position, int fScore)
    {
        Position = position;
        FScore = fScore;
    }
}