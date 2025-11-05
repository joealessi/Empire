public class Tank : LandUnit
{
    public Tank()
    {
        MaxPower = 8;
        MaxToughness = 7;
        MaxLife = 15;
        MaxMovementPoints = 3;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'T' : 't';
    public override string GetName() => "Tank";
}