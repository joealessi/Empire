public class PatrolBoat : SeaUnit
{
    public PatrolBoat()
    {
        MaxMovementPoints = 4;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 2;
        Defense = 2;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}