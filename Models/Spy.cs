public class Spy : LandUnit
{
    public bool IsRevealed { get; set; }
    
    public Spy()
    {
        MaxPower = 3;
        MaxToughness = 5;
        MaxLife = 8;
        MaxMovementPoints = 3;
        IsRevealed = false;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'Y' : 'y';
    public override string GetName() => "Spy";
}