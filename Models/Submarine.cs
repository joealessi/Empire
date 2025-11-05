public class Submarine : SeaUnit
{
    public bool IsSubmerged { get; set; }
    
    public Submarine()
    {
        MaxPower = 8;
        MaxToughness = 4;
        MaxLife = 12;
        MaxMovementPoints = 3;
        IsSubmerged = true;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'S' : 's';
    public override string GetName() => "Submarine";
}