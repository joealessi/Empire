public class Bomber : AirUnit
{
    public TilePosition? TargetPosition { get; set; }
    public List<TilePosition> FlightPath { get; set; }

    public Bomber()
    {
        MaxMovementPoints = 8;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        MaxFuel = 16;
        Fuel = MaxFuel;
        HomeBaseId = -1;
        Attack = 10;
        Defense = 1;
        FlightPath = new List<TilePosition>();
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return true;
    }
}