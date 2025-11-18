public class Artillery : LandUnit
{
    public int AttackRange { get; set; }

    public Artillery()
    {
        MaxMovementPoints = 1;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 8;
        Defense = 1;
        AttackRange = 4; // Can attack from 2 tiles away
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Land ||
               terrain == TerrainType.Plains ||
               terrain == TerrainType.Forest ||
               terrain == TerrainType.Hills;
    }
}