public class Transport : SeaUnit
{
    public int Capacity { get; set; }
    public List<LandUnit> EmbarkedUnits { get; set; }
    
    public Transport()
    {
        MaxPower = 2;
        MaxToughness = 4;
        MaxLife = 12;
        MaxMovementPoints = 3;
        Capacity = 6;
        EmbarkedUnits = new List<LandUnit>();
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'N' : 'n';
    public override string GetName() => "Transport";
    
    public bool CanEmbark(LandUnit unit)
    {
        return EmbarkedUnits.Count < Capacity;
    }
}