public class Transport : SeaUnit
{
    public const int MAX_CAPACITY = 6;
    public List<LandUnit> LoadedUnits { get; set; }

    public int Capacity => MAX_CAPACITY;
    public List<LandUnit> EmbarkedUnits => LoadedUnits;

    public Transport()
    {
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        MaxLife = 2;
        Life = MaxLife;
        LoadedUnits = new List<LandUnit>();
        Attack = 0;
        Defense = 2;
    }

    public bool CanLoad()
    {
        return LoadedUnits.Count < MAX_CAPACITY;
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.CoastalWater;
    }
}