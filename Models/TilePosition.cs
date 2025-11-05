public struct TilePosition
{
    public int X { get; set; }
    public int Y { get; set; }
    
    public TilePosition(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is TilePosition other)
            return X == other.X && Y == other.Y;
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }
    
    public static bool operator ==(TilePosition left, TilePosition right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(TilePosition left, TilePosition right)
    {
        return !left.Equals(right);
    }
}