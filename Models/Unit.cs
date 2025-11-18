public abstract class Unit
{
    public int UnitId { get; set; }
    public int OwnerId { get; set; }
    public TilePosition Position { get; set; }
    public double MovementPoints { get; set; }
    public double MaxMovementPoints { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public bool IsVeteran { get; set; }
    public Orders CurrentOrders { get; set; }
    public bool IsSkippedThisTurn { get; set; }
    public bool IsOnSentry { get; set; }

    public Unit()
    {
        CurrentOrders = new Orders();
        IsSkippedThisTurn = false;
        IsOnSentry = false;
    }

    public abstract bool CanMoveOn(TerrainType terrain);

    public void WakeUp()
    {
        IsOnSentry = false;
    }

    public string GetName()
    {
        return this.GetType().Name;
    }

    public void SkipThisTurn()
    {
        IsSkippedThisTurn = true;
    }

    public void Sleep()
    {
        IsOnSentry = true;
    }

    public void SetSentry()
    {
        IsOnSentry = true;
    }

    public bool IsAsleep
    {
        get { return IsOnSentry; }
    }

    // Legacy property names for backwards compatibility
    public int Power
    {
        get { return Attack; }
        set { Attack = value; }
    }

    public int MaxPower
    {
        get { return Attack; }
        set { Attack = value; }
    }

    public int Toughness
    {
        get { return Defense; }
        set { Defense = value; }
    }

    public int MaxToughness
    {
        get { return Defense; }
        set { Defense = value; }
    }

    public void AddExperience()
    {
        IsVeteran = true;
    }

    public bool CanEnterEnemyStructure(Tile tile)
    {
        if (tile.Structure == null)
            return true;
    
        // Can only enter enemy structures if they're destroyed
        if (tile.Structure.OwnerId != OwnerId && tile.Structure.Life > 0)
            return false;
    
        return true;
    }
}