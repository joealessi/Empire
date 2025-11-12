public class OrbitingSatellite : Satellite
{
    public OrbitType Orbit { get; set; }
    
    public OrbitingSatellite()
    {
        MaxMovementPoints = 0; // Moves automatically
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 0;
        Defense = 0;
        VisionRadius = 10;
        Orbit = OrbitType.Polar;
    }

    public TilePosition GetNextOrbitPosition(Map map)
    {
        int nextX = Position.X;
        int nextY = Position.Y;

        switch (Orbit)
        {
            case OrbitType.Horizontal:
                nextX++;
                if (nextX >= map.Width)
                    nextX = 0;
                break;

            case OrbitType.Vertical:
                nextY++;
                if (nextY >= map.Height)
                    nextY = 0;
                break;

            case OrbitType.RightDiagonal:
                nextX++;
                nextY++;
                if (nextX >= map.Width)
                    nextX = 0;
                if (nextY >= map.Height)
                    nextY = 0;
                break;

            case OrbitType.LeftDiagonal:
                nextX--;
                nextY++;
                if (nextX < 0)
                    nextX = map.Width - 1;
                if (nextY >= map.Height)
                    nextY = 0;
                break;

            case OrbitType.Polar:
                // Polar orbit goes vertically
                nextY++;
                if (nextY >= map.Height)
                {
                    nextY = 0;
                    nextX++;
                    if (nextX >= map.Width)
                        nextX = 0;
                }
                break;
        }

        return new TilePosition(nextX, nextY);
    }
}