public class Destroyer : SeaUnit
{
    public Destroyer()
    {
        MaxPower = 7;
        MaxToughness = 6;
        MaxLife = 15;
        MaxMovementPoints = 4;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'D' : 'd';
    public override string GetName() => "Destroyer";
}