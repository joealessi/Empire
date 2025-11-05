public abstract class Structure
{
    public int StructureId { get; set; }
    public TilePosition Position { get; set; }  
    public int OwnerId { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int VisionRange { get; set; }    
    public abstract char GetSymbol();
    public abstract string GetName();
}