public class Tanker : AirUnit
{
    public Tanker()
    {
        MaxPower = 0;
        MaxToughness = 2;
        MaxLife = 8;
        MaxMovementPoints = 4;
        MaxFuel = 20;
        HomeBaseId = -1;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
        Fuel = MaxFuel;
    }
    
    public override char GetSymbol() => IsVeteran ? 'K' : 'k';
    public override string GetName() => "Tanker";
    
    public override bool CanAttack(Unit target)
    {
        return false; // Tankers cannot attack
    }
}