using System.Collections.Generic;

// A mine built on a resource tile. Produces its resource each turn for the owner ONLY
// while it has a supply line to a friendly Base/City (see Game.UpdateSupplyLines). Has
// HP (from the resource definition) and no attack; destroyed by military, captured by Miners.
public class Mine : Structure
{
    public ResourceType Resource { get; set; }

    // Recomputed each turn / after moves by Game.UpdateSupplyLines.
    public bool IsConnected { get; set; }
    public List<TilePosition> SupplyPath { get; set; } = new List<TilePosition>();

    public Mine(ResourceType resource)
    {
        Resource = resource;
        var def = ResourceRegistry.Get(resource);
        MaxLife = def.MineHp;
        Life = MaxLife;
        VisionRange = 1;
    }

    public override char GetSymbol() => 'M';

    public override string GetName()
    {
        if (!string.IsNullOrWhiteSpace(CustomName))
            return CustomName;
        return ResourceRegistry.Get(Resource).DisplayName + " Mine";
    }

    public override int GetDefenseBonus() => 10;
}
