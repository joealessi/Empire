public class PatrolBoat : SeaUnit
{
    public PatrolBoat()
    {
        MaxPower = 4;
        MaxToughness = 3;
        MaxLife = 8;
        MaxMovementPoints = 5;
        
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
        MovementPoints = MaxMovementPoints;
    }
    
    public override char GetSymbol() => IsVeteran ? 'P' : 'p';
    public override string GetName() => "Patrol Boat";
    
    public void ApplyDeepWaterPenalty(bool inDeepWater)
    {
        if (inDeepWater)
        {
            Toughness = (int)(MaxToughness * 0.6);
            MovementPoints = (int)(MaxMovementPoints * 0.6);
        }
        else
        {
            Toughness = MaxToughness;
            MovementPoints = MaxMovementPoints;
        }
    }
}