public class Spy : LandUnit
{
    public bool IsRevealed { get; set; }

    public Spy()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        IsRevealed = false;
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