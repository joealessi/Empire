using System.Collections.Generic;
using System.Linq;

// Relative rarity of a resource — drives how many tiles get placed.
public enum ResourceScarcity
{
    Common,
    Uncommon,
    Rare
}

// Describes one resource/currency. Adding a new resource is a single entry in
// ResourceRegistry.Definitions plus its icon file — income, costs, the HUD, mines,
// the map renderer, and map generation (scarcity + terrain) all read from here.
public class ResourceDefinition
{
    public ResourceType Type { get; }
    public string DisplayName { get; }
    public bool IsMineable { get; }   // appears on tiles and can be mined
    public int MineHp { get; }        // HP of a mine built on this resource (mineable only)
    public int YieldPerTurn { get; }  // amount produced per connected mine / owned tile
    public string IconPath { get; }   // pack-relative image path, e.g. "/Resources/oil_16.png"
    public string ColorHex { get; }   // HUD text color
    public string Symbol { get; }     // short glyph for cost strings, e.g. "🛢️"
    public ResourceScarcity Scarcity { get; }              // how many spawn
    public IReadOnlyList<TerrainType> AllowedTerrain { get; } // where it may spawn

    public ResourceDefinition(ResourceType type, string displayName, bool isMineable,
                              int mineHp, int yieldPerTurn, string iconPath, string colorHex, string symbol,
                              ResourceScarcity scarcity, IReadOnlyList<TerrainType> allowedTerrain)
    {
        Type = type;
        DisplayName = displayName;
        IsMineable = isMineable;
        MineHp = mineHp;
        YieldPerTurn = yieldPerTurn;
        IconPath = iconPath;
        ColorHex = colorHex;
        Symbol = symbol;
        Scarcity = scarcity;
        AllowedTerrain = allowedTerrain ?? new TerrainType[0];
    }
}

public static class ResourceRegistry
{
    // The single source of truth for resource definitions. Order here is the order
    // currencies appear in the HUD. To add a resource: add an entry (and an icon).
    public static readonly IReadOnlyList<ResourceDefinition> Definitions = new List<ResourceDefinition>
    {
        new ResourceDefinition(ResourceType.Gold,  "Gold",  isMineable: false, mineHp: 0, yieldPerTurn: 1, "/Resources/gold_16.png",  "#FFD43B", "💰",
            scarcity: ResourceScarcity.Common, allowedTerrain: new TerrainType[0]),
        new ResourceDefinition(ResourceType.Steel, "Steel", isMineable: true,  mineHp: 8, yieldPerTurn: 1, "/Resources/steel_16.png", "#C0C0C0", "⚙️",
            scarcity: ResourceScarcity.Common, allowedTerrain: new[] { TerrainType.Hills, TerrainType.Mountain }),
        new ResourceDefinition(ResourceType.Oil,   "Oil",   isMineable: true,  mineHp: 3, yieldPerTurn: 1, "/Resources/oil_16.png",   "#FFA500", "🛢️",
            scarcity: ResourceScarcity.Common, allowedTerrain: new[] { TerrainType.Land, TerrainType.Plains, TerrainType.Forest, TerrainType.Hills, TerrainType.Mountain }),
    };

    // Base number of tiles for a 50x50 map; placement scales this by map dimension.
    public static int BaseCount(ResourceScarcity scarcity) => scarcity switch
    {
        ResourceScarcity.Common => 15,
        ResourceScarcity.Uncommon => 8,
        ResourceScarcity.Rare => 4,
        _ => 10
    };

    private static readonly Dictionary<ResourceType, ResourceDefinition> _byType =
        Definitions.ToDictionary(d => d.Type);

    // All spendable/stockpiled currencies, in display order.
    public static IEnumerable<ResourceType> Currencies => Definitions.Select(d => d.Type);

    // Mineable tile resources, in display order.
    public static IEnumerable<ResourceType> Mineable =>
        Definitions.Where(d => d.IsMineable).Select(d => d.Type);

    public static ResourceDefinition Get(ResourceType type) => _byType[type];

    public static bool IsMineable(ResourceType type) =>
        _byType.TryGetValue(type, out var d) && d.IsMineable;
}
