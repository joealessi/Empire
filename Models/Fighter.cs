public class Fighter : AirUnit
{
    public Fighter()
    {
        MaxMovementPoints = 10;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        MaxFuel = 20;
        Fuel = MaxFuel;
        HomeBaseId = -1;
        Attack = 3;
        Defense = 3;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return true;
    }
}