public class GeosynchronousSatellite : Satellite
{
    public GeosynchronousSatellite()
    {
        MaxPower = 0;
        MaxToughness = 3;
        MaxLife = 5;
        MaxMovementPoints = 0;
        VisionRadius = 15;
        Lifespan = 60;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = 0;
        TurnsRemaining = Lifespan;
    }
    
    public override char GetSymbol() => IsVeteran ? 'G' : 'g';
    public override string GetName() => "Geosync Satellite";
}