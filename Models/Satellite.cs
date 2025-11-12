public abstract class Satellite : Unit
{
    public int VisionRadius { get; set; }

    public override bool CanMoveOn(TerrainType terrain)
    {
        return true; // Satellites can move anywhere
    }
}