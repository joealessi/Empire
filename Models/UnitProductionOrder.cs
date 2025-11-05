public class UnitProductionOrder
{
    public Type UnitType { get; set; }
    public int TotalCost { get; set; }
    public string DisplayName { get; set; }
    
    public UnitProductionOrder(Type unitType, int cost, string displayName)
    {
        UnitType = unitType;
        TotalCost = cost;
        DisplayName = displayName;
    }
}