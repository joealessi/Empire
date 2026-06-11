public abstract class Structure
{
    public int StructureId { get; set; }
    public TilePosition Position { get; set; }  
    public int OwnerId { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int VisionRange { get; set; }
    public int TurnsSinceLastHeal { get; set; }
    public string CustomName { get; set; }

    // Populace living at this structure. Grows each turn (City +2, Base +1, Mine +0.5) and
    // is spent building people-units (Army 4, Miner 2); never allowed below 1.
    public double Population { get; set; }

    public Structure()
    {
        TurnsSinceLastHeal = 0;
        Population = 1;
    }
    
    public abstract char GetSymbol();
    
    public virtual string GetName()
    {
        if (!string.IsNullOrWhiteSpace(CustomName))
            return CustomName;
        return this is City ? "City" : "Base";
    }
    
    public virtual int GetDefenseBonus()
    {
        return this is City ? 75 : 50;
    }

    public bool IsDestroyed()
    {
        return Life <= 0;
    }

    public void Heal(int amount)
    {
        Life = Math.Min(Life + amount, MaxLife);
    }
}