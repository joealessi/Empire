public class Base : Structure
{
    public bool CanProduceNaval { get; set; }
    public bool HasShipyard { get; set; }
    public Queue<UnitProductionOrder> ProductionQueue { get; set; }
    public int ProductionPointsPerTurn { get; set; }
    public double CurrentProductionProgress { get; set; }
    
    // Unit storage with capacity limits
    public List<AirUnit> Airport { get; set; }
    public List<Unit> MotorPool { get; set; }
    public List<LandUnit> Barracks { get; set; }
    public List<SeaUnit> Shipyard { get; set; }
    
    public const int MAX_BARRACKS_CAPACITY = 10;
    public const int MAX_AIRPORT_CAPACITY = 6;
    public const int MAX_SHIPYARD_CAPACITY = 3;
    
    // Units being repaired
    public Dictionary<Unit, int> UnitsBeingRepaired { get; set; }
    
    public Base()
    {
        MaxLife = 40;
        Life = MaxLife;
        VisionRange = 5;
        ProductionPointsPerTurn = 10;
        ProductionQueue = new Queue<UnitProductionOrder>();
        CurrentProductionProgress = 0;
        Airport = new List<AirUnit>();
        MotorPool = new List<Unit>();
        Barracks = new List<LandUnit>();
        Shipyard = new List<SeaUnit>();
        UnitsBeingRepaired = new Dictionary<Unit, int>();
        CanProduceNaval = false;
        HasShipyard = false;
    }
    
    public override char GetSymbol() => 'B';

    public override int GetDefenseBonus()
    {
        // Barracks and motor-pool units garrison the base, raising its defense
        int bonus = 50;
        int garrisonCount = Barracks.Count + MotorPool.Count;
        bonus += garrisonCount * 3;
        return bonus;
    }
    
    public int GetAirportSpaceUsed()
    {
        int used = Airport.Count;
        // Count aircraft under construction
        foreach (var order in ProductionQueue)
        {
            if (order.UnitType == typeof(Fighter) || 
                order.UnitType == typeof(Bomber) || 
                order.UnitType == typeof(Tanker))
            {
                used++;
            }
        }
        return used;
    }
    
    public int GetShipyardSpaceUsed()
    {
        int used = Shipyard.Count;
        // Count ships under construction
        foreach (var order in ProductionQueue)
        {
            if (order.UnitType == typeof(Carrier) || 
                order.UnitType == typeof(Battleship) || 
                order.UnitType == typeof(Destroyer) ||
                order.UnitType == typeof(Submarine) ||
                order.UnitType == typeof(PatrolBoat) ||
                order.UnitType == typeof(Transport))
            {
                used++;
            }
        }
        return used;
    }
    
    public int GetBarracksSpaceUsed()
    {
        int used = Barracks.Count;
        // Count infantry units under construction
        foreach (var order in ProductionQueue)
        {
            if (order.UnitType == typeof(Army) ||
                order.UnitType == typeof(Sapper) ||
                order.UnitType == typeof(Spy) ||
                order.UnitType == typeof(Miner))
            {
                used++;
            }
        }
        return used;
    }
    
    public bool CanBuildUnit(Type unitType)
    {
        if (unitType == typeof(Fighter) || unitType == typeof(Bomber) || unitType == typeof(Tanker))
        {
            return GetAirportSpaceUsed() < MAX_AIRPORT_CAPACITY;
        }
        else if (unitType == typeof(Carrier) || unitType == typeof(Battleship) || 
                 unitType == typeof(Destroyer) || unitType == typeof(Submarine) ||
                 unitType == typeof(PatrolBoat) || unitType == typeof(Transport))
        {
            return HasShipyard && GetShipyardSpaceUsed() < MAX_SHIPYARD_CAPACITY;
        }
        else if (unitType == typeof(Army) || unitType == typeof(Sapper) || unitType == typeof(Spy) || unitType == typeof(Miner))
        {
            return GetBarracksSpaceUsed() < MAX_BARRACKS_CAPACITY;
        }
        
        return true; // Other units (tanks, artillery, etc.) have no capacity limit
    }
}
