using System;
using System.Collections.Generic;

public class UnitProductionOrder
{
    public Type UnitType { get; set; }
    public int TotalCost { get; set; }
    public string DisplayName { get; set; }

    // Per-resource cost for this order (dynamic — keyed by ResourceType).
    public Dictionary<ResourceType, int> Cost { get; set; }

    // Convenience facades over the cost map so existing call sites keep working.
    public int GoldCost => Cost.TryGetValue(ResourceType.Gold, out var v) ? v : 0;
    public int SteelCost => Cost.TryGetValue(ResourceType.Steel, out var v) ? v : 0;
    public int OilCost => Cost.TryGetValue(ResourceType.Oil, out var v) ? v : 0;

    // Compact helper for the three current currencies.
    private static Dictionary<ResourceType, int> C(int gold, int steel, int oil) =>
        new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, gold },
            { ResourceType.Steel, steel },
            { ResourceType.Oil, oil },
        };

    // Resource-agnostic cost: list only the resources a unit actually uses. Adding a new
    // resource (e.g. Uranium) needs no change here — just add a pair to the units that use it.
    //   e.g. Price((ResourceType.Gold, 14), (ResourceType.Steel, 7), (ResourceType.Uranium, 2))
    private static Dictionary<ResourceType, int> Price(params (ResourceType type, int amount)[] items)
    {
        var d = new Dictionary<ResourceType, int>();
        foreach (var (type, amount) in items)
            d[type] = amount;
        return d;
    }

    // Canonical build-cost table — the single source of truth, read by both the player
    // build menu and the AI. Update unit costs here only.
    public static readonly Dictionary<Type, Dictionary<ResourceType, int>> Costs =
        new Dictionary<Type, Dictionary<ResourceType, int>>
        {
            { typeof(Army),                    C(2, 0, 0) },
            { typeof(Miner),                   C(2, 0, 0) },
            { typeof(Tank),                    C(3, 2, 0) },
            { typeof(Artillery),               C(4, 3, 0) },
            { typeof(Sapper),                  C(3, 2, 0) },
            { typeof(AntiAircraft),            C(2, 1, 0) },
            { typeof(Spy),                     C(3, 0, 0) },
            { typeof(Fighter),                 C(4, 4, 1) },
            { typeof(Bomber),                  C(6, 8, 2) },
            { typeof(Tanker),                  C(3, 1, 1) },
            { typeof(PatrolBoat),              C(2, 1, 0) },
            { typeof(Destroyer),               C(3, 2, 1) },
            { typeof(Submarine),               C(6, 3, 2) },
            { typeof(Carrier),                 C(8, 4, 3) },
            { typeof(Battleship),              C(9, 5, 3) },
            { typeof(Transport),               C(3, 1, 2) },
            { typeof(OrbitingSatellite),       C(5, 2, 1) },
            { typeof(GeosynchronousSatellite), C(9, 5, 3) },
        };

    // Populace consumed to build a people-unit (0 for machines). Never drops a structure below 1.
    public static int PopulationCost(Type unitType) =>
        unitType == typeof(Army) || unitType == typeof(Spy) ? 4 :
        unitType == typeof(Miner) ? 2 : 0;

    // Canonical cost for a unit type as a fresh map (empty if not listed).
    public static Dictionary<ResourceType, int> GetCost(Type unitType) =>
        Costs.TryGetValue(unitType, out var c)
            ? new Dictionary<ResourceType, int>(c)
            : new Dictionary<ResourceType, int>();

    // Primary constructor: explicit per-resource cost map.
    public UnitProductionOrder(Type unitType, Dictionary<ResourceType, int> cost, string displayName)
    {
        UnitType = unitType;
        Cost = cost ?? new Dictionary<ResourceType, int>();
        DisplayName = displayName;
        TotalCost = GoldCost * 10;
    }

    // Cost-table-driven constructor: pulls the canonical cost for the unit type.
    public UnitProductionOrder(Type unitType, string displayName)
        : this(unitType, GetCost(unitType), displayName)
    {
    }

    // Backward compatibility: explicit gold/steel/oil amounts.
    public UnitProductionOrder(Type unitType, int goldCost, int steelCost, int oilCost, string displayName)
        : this(unitType, C(goldCost, steelCost, oilCost), displayName)
    {
    }

    // Backward compatibility: cost expressed in production points.
    public UnitProductionOrder(Type unitType, int cost, string displayName)
        : this(unitType, C(cost / 10, 0, 0), displayName)
    {
    }
}
