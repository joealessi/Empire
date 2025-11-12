public class AntiAircraft : LandUnit
{
    public AntiAircraft()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 2;
        Defense = 2;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Land ||
               terrain == TerrainType.Plains ||
               terrain == TerrainType.Forest ||
               terrain == TerrainType.Hills;
    }
}