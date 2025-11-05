public class Army : LandUnit
{
    public Army()
    {
        MaxPower = 5;
        MaxToughness = 5;
        MaxLife = 10;
        MaxMovementPoints = 2;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'A' : 'a';
    public override string GetName() => "Army";
}