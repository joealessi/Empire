using System;
using System.Collections.Generic;

public class UnitProductionOrder
{
    public Type UnitType { get; set; }
    public int TotalCost { get; set; }
    public string DisplayName { get; set; }

    // NEW: Individual resource costs
    public int GoldCost { get; set; }
    public int SteelCost { get; set; }
    public int OilCost { get; set; }

    // Canonical build-cost table (gold, steel, oil) — the single source of truth
    // shared by the player build menu (MainWindow.PopulateAvailableUnits) and the
    // AI production logic (AIController.DecideWhatToBuild). Update costs here only.
    public static readonly Dictionary<Type, (int gold, int steel, int oil)> Costs =
        new Dictionary<Type, (int gold, int steel, int oil)>
        {
            { typeof(Army),                    (2, 0, 0) },
            { typeof(Tank),                    (3, 2, 0) },
            { typeof(Artillery),               (3, 2, 0) },
            { typeof(Sapper),                  (2, 1, 0) },
            { typeof(AntiAircraft),            (2, 1, 0) },
            { typeof(Spy),                     (3, 0, 0) },
            { typeof(Fighter),                 (3, 1, 1) },
            { typeof(Bomber),                  (6, 3, 2) },
            { typeof(Tanker),                  (3, 1, 1) },
            { typeof(PatrolBoat),              (2, 1, 0) },
            { typeof(Destroyer),               (3, 2, 1) },
            { typeof(Submarine),               (5, 2, 1) },
            { typeof(Carrier),                 (8, 4, 3) },
            { typeof(Battleship),              (9, 5, 3) },
            { typeof(Transport),               (2, 1, 1) },
            { typeof(OrbitingSatellite),       (8, 4, 2) },
            { typeof(GeosynchronousSatellite), (14, 7, 5) },
        };

    // Look up the canonical cost for a unit type (returns 0/0/0 if not listed).
    public static (int gold, int steel, int oil) GetCost(Type unitType)
    {
        return Costs.TryGetValue(unitType, out var cost) ? cost : (0, 0, 0);
    }

    public UnitProductionOrder(Type unitType, int goldCost, int steelCost, int oilCost, string displayName)
    {
        UnitType = unitType;
        GoldCost = goldCost;
        SteelCost = steelCost;
        OilCost = oilCost;
        DisplayName = displayName;
        TotalCost = goldCost * 10;
    }

    // Cost-table-driven constructor: pulls the canonical cost for the unit type.
    public UnitProductionOrder(Type unitType, string displayName)
        : this(unitType, GetCost(unitType).gold, GetCost(unitType).steel, GetCost(unitType).oil, displayName)
    {
    }

    // Backward compatibility constructor
    public UnitProductionOrder(Type unitType, int cost, string displayName)
        : this(unitType, cost / 10, 0, 0, displayName)
    {
    }
}
