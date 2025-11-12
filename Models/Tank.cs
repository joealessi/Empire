public class Tank : LandUnit
{
    public Tank()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 5;
        Defense = 3;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Land ||
               terrain == TerrainType.Plains ||
               terrain == TerrainType.Forest ||
               terrain == TerrainType.Hills;
    }
}