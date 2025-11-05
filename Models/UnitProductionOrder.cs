public class UnitProductionOrder
{
    public Type UnitType { get; set; }
    public int TotalCost { get; set; }  
    public string DisplayName { get; set; }
    
    // NEW: Individual resource costs
    public int GoldCost { get; set; }
    public int SteelCost { get; set; }
    public int OilCost { get; set; }
    
    public UnitProductionOrder(Type unitType, int goldCost, int steelCost, int oilCost, string displayName)
    {
        UnitType = unitType;
        GoldCost = goldCost;
        SteelCost = steelCost;
        OilCost = oilCost;
        DisplayName = displayName;
        TotalCost = goldCost * 10;
    }
    
    // Backward compatibility constructor
    public UnitProductionOrder(Type unitType, int cost, string displayName)
        : this(unitType, cost / 10, 0, 0, displayName)
    {
    }
}