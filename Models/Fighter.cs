public class Fighter : AirUnit
{
    public Fighter()
    {
        MaxPower = 6;
        MaxToughness = 4;
        MaxLife = 8;
        MaxMovementPoints = 5;
        MaxFuel = 10;
        HomeBaseId = -1;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
        Fuel = MaxFuel;
    }
    
    public override char GetSymbol() => IsVeteran ? 'F' : 'f';
    public override string GetName() => "Fighter";
}