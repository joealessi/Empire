public class Destroyer : SeaUnit
{
    public Destroyer()
    {
        MaxMovementPoints = 3;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        Attack = 4;
        Defense = 3;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}