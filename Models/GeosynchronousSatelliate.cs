public class GeosynchronousSatellite : Satellite
{
    public OrbitType Orbit { get; set; }
    
    public GeosynchronousSatellite()
    {
        MaxMovementPoints = 0; // Stationary
        MovementPoints = MaxMovementPoints;
        MaxLife = 1;
        Life = MaxLife;
        Attack = 0;
        Defense = 0;
        VisionRadius = 15;
        Orbit = OrbitType.Geosynchronous;
    }
}