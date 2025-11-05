public class Bomber : AirUnit
{
    public bool BombsDropped { get; set; }
    public TilePosition TargetPosition { get; set; }  // Changed from Point
    public List<TilePosition> FlightPath { get; set; }  // Changed from List<Point>
    public int CurrentPathIndex { get; set; }
    
    public Bomber()
    {
        MaxPower = 15;
        MaxToughness = 0;
        MaxLife = 10;
        MaxMovementPoints = 4;
        MaxFuel = 20;
        HomeBaseId = -1;
        BombsDropped = false;
        FlightPath = new List<TilePosition>();
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
        Fuel = MaxFuel;
    }
    
    public override char GetSymbol() => IsVeteran ? 'B' : 'b';
    public override string GetName() => "Bomber";
    
    public override bool CanAttack(Unit target)
    {
        // Bombers can only attack ground units and structures
        return !(target is AirUnit);
    }
}