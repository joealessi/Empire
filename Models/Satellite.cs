public enum OrbitType
{
    None,
    Horizontal,
    Vertical,
    RightDiagonal,
    LeftDiagonal
}

public abstract class Satellite : Unit
{
    public int Lifespan { get; set; }
    public int TurnsRemaining { get; set; }
    public int VisionRadius { get; set; }
    
    public Satellite()
    {
        MaxMovementPoints = 0;
        MovementPoints = 0;
    }
    
    public virtual void AgeSatellite()
    {
        TurnsRemaining--;
        if (TurnsRemaining <= 0)
        {
            Life = 0;
        }
    }
    
    public override bool CanAttack(Unit target)
    {
        return false;
    }
}