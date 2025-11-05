public class Battleship : SeaUnit
{
    public Battleship()
    {
        MaxPower = 12;
        MaxToughness = 10;
        MaxLife = 25;
        MaxMovementPoints = 2;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'L' : 'l';
    public override string GetName() => "Battleship";
}