public class Army : LandUnit
{
    public Army()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        Attack = 1;
        Defense = 1;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Land ||
               terrain == TerrainType.Plains ||
               terrain == TerrainType.Forest ||
               terrain == TerrainType.Hills ||
               terrain == TerrainType.Mountain;
    }
}