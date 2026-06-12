public class ASATSatellite : Satellite
{
    public const int InterceptRange = 10;

    public ASATSatellite()
    {
        MaxMovementPoints = 0;
        MovementPoints = 0;
        MaxLife = 1;
        Life = 1;
        Attack = 0;
        Defense = 0;
        VisionRadius = 2;
    }

    public TilePosition GetNextOrbitPosition(Map map)
    {
        int nextX = Position.X;
        int nextY = Position.Y + 1;
        if (nextY >= map.Height)
        {
            nextY = 0;
            nextX++;
            if (nextX >= map.Width)
                nextX = 0;
        }
        return new TilePosition(nextX, nextY);
    }
}
