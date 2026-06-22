public abstract class Structure
{
    public int StructureId { get; set; }
    public TilePosition Position { get; set; }  
    public int OwnerId { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int VisionRange { get; set; }
    public int TurnsSinceLastHeal { get; set; }
    public int LastAttackedTurn { get; set; }
    public bool IsUnderFullSiege { get; set; }
    public string CustomName { get; set; }

    // Populace living at this structure. Grows each turn (City +2, Base +1, Mine +0.5) and
    // is spent building people-units (Army 4, Miner 2); never allowed below 1.
    public double Population { get; set; }

    // One-time civic upgrades (bought with populace) and their permanent effects.
    public bool HasIndustry { get; set; }       // +production
    public bool HasFortifications { get; set; } // +MaxLife
    public bool HasWatchtower { get; set; }     // +vision
    public bool HasHousing { get; set; }        // +populace growth
    public bool HasTreasury { get; set; }       // +gold/turn
    public double ProductionBonus { get; set; } // fraction added to production points/turn
    public double GrowthBonus { get; set; }     // extra populace/turn
    public int GoldBonus { get; set; }          // extra gold/turn

    public Structure()
    {
        TurnsSinceLastHeal = 0;
        LastAttackedTurn = -999; // never attacked — healing starts immediately
        Population = 1;
    }
    
    public abstract char GetSymbol();
    
    public virtual string GetName()
    {
        if (!string.IsNullOrWhiteSpace(CustomName))
            return CustomName;
        // Fallback using position so it's never just "City" or "Base"
        return this is City ? $"City ({Position.X},{Position.Y})" : $"Base ({Position.X},{Position.Y})";
    }
    
    public virtual int GetDefenseBonus()
    {
        return 50; // fallback for structures without subclass override
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