public class Artillery : LandUnit
{
    public int AttackRange { get; set; }
    
    public Artillery()
    {
        MaxPower = 12;
        MaxToughness = 3;
        MaxLife = 8;
        MaxMovementPoints = 1;
        AttackRange = 3;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'R' : 'r';
    public override string GetName() => "Artillery";
}