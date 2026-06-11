using System;

// Single source of truth for civic upgrade costs and effects. Used by both the player
// UI (MainWindow) and the AI (AIController) so costs/effects never drift.
public static class CivicUpgrades
{
    public const int CostIndustry = 15, CostFortify = 15, CostWatchtower = 12, CostHousing = 30,
                     CostTreasury = 30, CostMilitary1 = 40, CostMilitary2 = 40, CostConscript = 8,
                     CostRepair = 12, SteelCostMilitary2 = 10;

    // Spend populace if it can stay >= 1.
    private static bool SpendPop(Structure s, int cost)
    {
        if (s.Population - cost < 1) return false;
        s.Population -= cost;
        return true;
    }

    public static bool BuyIndustry(Structure s)
    {
        if (s.HasIndustry || !SpendPop(s, CostIndustry)) return false;
        s.HasIndustry = true;
        s.ProductionBonus += 0.0125;
        return true;
    }

    public static bool BuyFortify(Structure s)
    {
        if (s.HasFortifications || !SpendPop(s, CostFortify)) return false;
        s.HasFortifications = true;
        s.MaxLife += 5;
        s.Heal(5);
        return true;
    }

    public static bool BuyWatchtower(Structure s)
    {
        if (s.HasWatchtower || !SpendPop(s, CostWatchtower)) return false;
        s.HasWatchtower = true;
        s.VisionRange += 1;
        return true;
    }

    public static bool BuyHousing(Structure s)
    {
        if (s.HasHousing || !SpendPop(s, CostHousing)) return false;
        s.HasHousing = true;
        s.GrowthBonus += 0.5;
        return true;
    }

    public static bool BuyTreasury(Structure s)
    {
        if (s.HasTreasury || !SpendPop(s, CostTreasury)) return false;
        s.HasTreasury = true;
        s.GoldBonus += 1;
        return true;
    }

    public static bool BuyMilitary1(Player p, Structure s)
    {
        if (p.HasMilitary1 || !SpendPop(s, CostMilitary1)) return false;
        p.HasMilitary1 = true;
        p.ArmyHealthBonus += 1;
        foreach (var u in p.Units)
            if (u is Army) { u.MaxLife += 1; u.Life = Math.Min(u.Life + 1, u.MaxLife); }
        return true;
    }

    public static bool BuyMilitary2(Player p, Structure s)
    {
        if (p.HasMilitary2 || !p.HasMilitary1) return false;
        if (p.GetResource(ResourceType.Steel) < SteelCostMilitary2) return false;
        if (!SpendPop(s, CostMilitary2)) return false;
        p.AddResource(ResourceType.Steel, -SteelCostMilitary2);
        p.HasMilitary2 = true;
        p.TankHealthBonus += 1;
        foreach (var u in p.Units)
            if (u is Tank) { u.MaxLife += 1; u.Life = Math.Min(u.Life + 1, u.MaxLife); }
        return true;
    }

    public static bool Repair(Structure s)
    {
        if (s.Life >= s.MaxLife || !SpendPop(s, CostRepair)) return false;
        s.Life = s.MaxLife;
        return true;
    }

    public static Army Conscript(Game game, Player p, Structure s)
    {
        TilePosition pos = game.FindAdjacentEmptyTile(s.Position);
        if (pos.X == -1) return null;
        if (!SpendPop(s, CostConscript)) return null;
        var army = new Army { OwnerId = p.PlayerId, Position = pos, MovementPoints = 0 };
        game.ApplyMilitaryUpgrades(army, p.PlayerId);
        game.Map.GetTile(pos).Units.Add(army);
        p.Units.Add(army);
        return army;
    }
}
