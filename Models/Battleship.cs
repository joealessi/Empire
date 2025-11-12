public class Battleship : SeaUnit
{
    public Battleship()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 3;
        Life = MaxLife;
        Attack = 10;
        Defense = 5;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}