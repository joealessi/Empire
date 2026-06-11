// Cheap, weak, non-combat land unit. Builds mines on resource tiles (consumed in the
// process) and can capture enemy mines by entering them.
public class Miner : LandUnit
{
    public Miner()
    {
        MaxMovementPoints = 3;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        Attack = 0;
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
