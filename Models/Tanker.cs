public class Tanker : AirUnit
{
    public Tanker()
    {
        MaxMovementPoints = 8;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        MaxFuel = 24;
        Fuel = MaxFuel;
        HomeBaseId = -1;
        Attack = 0;
        Defense = 1;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return true;
    }
}