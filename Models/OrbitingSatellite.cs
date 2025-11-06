public class OrbitingSatellite : Satellite
{
    public OrbitType Orbit { get; set; }
    
    public OrbitingSatellite()
    {
        MaxPower = 0;
        MaxToughness = 2;
        MaxLife = 3;
        MaxMovementPoints = 0;
        VisionRadius = 11;
        Lifespan = 40;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = 0;
        TurnsRemaining = Lifespan;
        
        Orbit = OrbitType.None;
    }
    
    public override char GetSymbol() => IsVeteran ? 'O' : 'o';
    public override string GetName() => $"Orbiting Satellite ({GetOrbitName()})";
    
    private string GetOrbitName()
    {
        return Orbit switch
        {
            OrbitType.Horizontal => "Horizontal",
            OrbitType.Vertical => "Vertical",
            OrbitType.RightDiagonal => "Right Diagonal",
            OrbitType.LeftDiagonal => "Left Diagonal",
            _ => "Unknown"
        };
    }
    
    public TilePosition GetNextOrbitPosition(Map map)
    {
        TilePosition nextPos = Position;
        
        switch (Orbit)
        {
            case OrbitType.Horizontal:
                nextPos = new TilePosition(Position.X + 5, Position.Y);
                if (nextPos.X >= map.Width)
                {
                    nextPos = new TilePosition(nextPos.X - map.Width, Position.Y);
                }
                break;
                
            case OrbitType.Vertical:
                nextPos = new TilePosition(Position.X, Position.Y + 5);
                if (nextPos.Y >= map.Height)
                {
                    nextPos = new TilePosition(Position.X, nextPos.Y - map.Height);
                }
                break;
                
            case OrbitType.RightDiagonal:
                nextPos = new TilePosition(Position.X + 4, Position.Y + 4);
                if (nextPos.X >= map.Width)
                {
                    nextPos = new TilePosition(nextPos.X - map.Width, nextPos.Y);
                }
                if (nextPos.Y >= map.Height)
                {
                    nextPos = new TilePosition(nextPos.X, nextPos.Y - map.Height);
                }
                break;
                
            case OrbitType.LeftDiagonal:
                nextPos = new TilePosition(Position.X - 4, Position.Y + 4);
                if (nextPos.X < 0)
                {
                    nextPos = new TilePosition(nextPos.X + map.Width, nextPos.Y);
                }
                if (nextPos.Y >= map.Height)
                {
                    nextPos = new TilePosition(nextPos.X, nextPos.Y - map.Height);
                }
                break;
        }
        
        return nextPos;
    }
}