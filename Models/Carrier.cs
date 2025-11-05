public class Carrier : SeaUnit
{
    public int Capacity { get; set; }
    public List<AirUnit> DockedAircraft { get; set; }
    
    public Carrier()
    {
        MaxPower = 6;
        MaxToughness = 8;
        MaxLife = 20;
        MaxMovementPoints = 3;
        Capacity = 6;
        DockedAircraft = new List<AirUnit>();
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'C' : 'c';
    public override string GetName() => "Carrier";
    
    public bool CanDock(AirUnit aircraft)
    {
        return DockedAircraft.Count < Capacity && !(aircraft is Bomber);
    }
}