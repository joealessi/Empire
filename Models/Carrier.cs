public class Carrier : SeaUnit
{
    public const int MAX_CAPACITY = 8;
    public List<AirUnit> DockedAircraft { get; set; }

    public int Capacity => MAX_CAPACITY;

    public Carrier()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 3;
        Life = MaxLife;
        DockedAircraft = new List<AirUnit>();
        Attack = 2;
        Defense = 4;
    }

    public bool CanDock(AirUnit aircraft)
    {
        return DockedAircraft.Count < MAX_CAPACITY;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}