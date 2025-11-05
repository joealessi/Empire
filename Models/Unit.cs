// Enums for game state

using System.Windows;

// Combat and movement support

// Base Unit class
public abstract class Unit
{
    public int UnitId { get; set; }
    public TilePosition Position { get; set; }
    public int OwnerId { get; set; }
    public bool IsVeteran { get; set; }
    public int Experience { get; set; }
    
    // Combat stats
    public int Power { get; set; }
    public int MaxPower { get; set; }
    public int Toughness { get; set; }
    public int MaxToughness { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    
    // Movement - changed to double for diagonal movement
    public double MovementPoints { get; set; }
    public int MaxMovementPoints { get; set; }
    
    // Orders
    public Orders CurrentOrders { get; set; }
    
    // Unit state flags for Skip/Sleep/Sentry
    public bool IsSkippedThisTurn { get; set; }
    public bool IsAsleep { get; set; }
    public bool IsOnSentry { get; set; }
    
    // Display
    public abstract char GetSymbol();
    public abstract string GetName();
    
    public Unit()
    {
        CurrentOrders = new Orders();
        IsSkippedThisTurn = false;
        IsAsleep = false;
        IsOnSentry = false;
    }
    
    // Unit state management methods
    public void WakeUp()
    {
        IsAsleep = false;
        IsOnSentry = false;
    }
    
    public void SkipThisTurn()
    {
        IsSkippedThisTurn = true;
    }
    
    public void Sleep()
    {
        IsAsleep = true;
        IsOnSentry = false;
    }
    
    public void SetSentry()
    {
        IsAsleep = true;
        IsOnSentry = true;
    }
    
    public void AddExperience()
    {
        Experience++;
        if (Experience >= 3 && !IsVeteran)
        {
            PromoteToVeteran();
        }
    }
    
    protected virtual void PromoteToVeteran()
    {
        IsVeteran = true;
        MaxPower = (int)(MaxPower * 1.2);
        MaxToughness = (int)(MaxToughness * 1.2);
        MaxLife = (int)(MaxLife * 1.2);
        Power = MaxPower;
        Toughness = MaxToughness;
        Life = MaxLife;
    }
    
    public virtual bool CanAttack(Unit target)
    {
        return true;
    }
}