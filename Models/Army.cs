public class Army : LandUnit
{
    public Army()
    {
        MaxMovementPoints = 1;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
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