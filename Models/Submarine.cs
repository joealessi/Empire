public class Submarine : SeaUnit
{
    public bool IsSubmerged { get; set; }

    public Submarine()
    {
        MaxMovementPoints = 3;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        Attack = 8;
        Defense = 2;
        IsSubmerged = false;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}