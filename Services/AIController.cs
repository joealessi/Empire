using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpireGame
{
    public class AIController
    {
        private Game game;
        private Random rand;
        private AIPlaystyle playstyle;
        private Action<string, MessageType> messageCallback;

        // ─── Strategy weights ────────────────────────────────────────────────────
        // Each weight is 0–1 and drives how eagerly the AI pursues that activity.
        private struct StrategyWeights
        {
            public float Assault;    // Drive units toward enemy structures
            public float War;        // Engage enemy units proactively
            public float Defense;    // Garrison own structures against threats
            public float Expansion;  // Build new bases via sappers
            public float Resources;  // Acquire mines / send miners
            public float Explore;    // Explore unknown map
        }

        private StrategyWeights GetStrategyWeights() => playstyle switch
        {
            AIPlaystyle.Aggressive => new StrategyWeights { Assault=1.0f, War=0.9f, Defense=0.3f, Expansion=0.5f, Resources=0.4f, Explore=0.3f },
            AIPlaystyle.Defensive  => new StrategyWeights { Assault=0.3f, War=0.5f, Defense=1.0f, Expansion=0.4f, Resources=0.7f, Explore=0.3f },
            AIPlaystyle.Buildup    => new StrategyWeights { Assault=0.3f, War=0.3f, Defense=0.7f, Expansion=1.0f, Resources=1.0f, Explore=0.5f },
            AIPlaystyle.Naval      => new StrategyWeights { Assault=0.7f, War=0.7f, Defense=0.5f, Expansion=0.6f, Resources=0.5f, Explore=0.6f },
            AIPlaystyle.Aerial     => new StrategyWeights { Assault=0.8f, War=0.7f, Defense=0.5f, Expansion=0.5f, Resources=0.4f, Explore=0.5f },
            _                      => new StrategyWeights { Assault=0.6f, War=0.6f, Defense=0.6f, Expansion=0.6f, Resources=0.6f, Explore=0.6f },
        };

        // ─── Construction / public API ────────────────────────────────────────────
        public AIController(Game game, AIPlaystyle playstyle = AIPlaystyle.Balanced)
        {
            this.game = game;
            this.rand = new Random();
            this.playstyle = playstyle;
        }

        public void SetPlaystyle(AIPlaystyle newPlaystyle) => playstyle = newPlaystyle;

        public void ExecuteAITurn(Player aiPlayer, Action<string, MessageType> onMessage = null)
        {
            messageCallback = onMessage;
            if (aiPlayer.Personality != null) playstyle = aiPlayer.Personality.Playstyle;

            HandleProduction(aiPlayer);
            HandleUnits(aiPlayer);
            HandleSappers(aiPlayer);
            game.CheckForEliminatedPlayers();
        }

        public void ExecuteAITurn(Player aiPlayer)
        {
            if (aiPlayer.Personality != null) playstyle = aiPlayer.Personality.Playstyle;

            HandleProduction(aiPlayer);
            HandleUnits(aiPlayer);
            HandleMining(aiPlayer);
            HandleCivicUpgrades(aiPlayer);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  CIVIC UPGRADES
        // ═══════════════════════════════════════════════════════════════════════════
        private void HandleCivicUpgrades(Player ai)
        {
            string[] priority;
            switch (playstyle)
            {
                case AIPlaystyle.Aggressive: priority = new[] { "mil1", "mil2", "industry", "conscript" }; break;
                case AIPlaystyle.Defensive:  priority = new[] { "fortify", "watchtower", "mil1", "repair" }; break;
                case AIPlaystyle.Buildup:    priority = new[] { "housing", "treasury", "industry" }; break;
                case AIPlaystyle.Naval:      priority = new[] { "treasury", "industry", "hightech" }; break;
                case AIPlaystyle.Aerial:     priority = new[] { "industry", "hightech", "watchtower" }; break;
                default:                     priority = new[] { "industry", "housing", "mil1" }; break;
            }

            foreach (var s in ai.Structures.ToList())
            {
                if (!(s is Base || s is City)) continue;
                foreach (var key in priority)
                    if (TryBuyUpgrade(key, s, ai)) break;
            }
        }

        private bool TryBuyUpgrade(string key, Structure s, Player ai)
        {
            const int buffer = 6;
            switch (key)
            {
                case "industry":   return s.Population >= CivicUpgrades.CostIndustry   + buffer && CivicUpgrades.BuyIndustry(s);
                case "fortify":    return s.Population >= CivicUpgrades.CostFortify    + buffer && CivicUpgrades.BuyFortify(s);
                case "watchtower": return s.Population >= CivicUpgrades.CostWatchtower + buffer && CivicUpgrades.BuyWatchtower(s);
                case "housing":    return s.Population >= CivicUpgrades.CostHousing    + buffer && CivicUpgrades.BuyHousing(s);
                case "treasury":   return s.Population >= CivicUpgrades.CostTreasury   + buffer && CivicUpgrades.BuyTreasury(s);
                case "mil1":       return s.Population >= CivicUpgrades.CostMilitary1  + buffer && CivicUpgrades.BuyMilitary1(ai, s);
                case "mil2":       return s.Population >= CivicUpgrades.CostMilitary2  + buffer && CivicUpgrades.BuyMilitary2(ai, s);
                case "hightech":   return s.Population >= CivicUpgrades.CostHighTechnology + buffer &&
                                          ai.GetResource(ResourceType.Oil) >= CivicUpgrades.OilCostHighTechnology &&
                                          CivicUpgrades.BuyHighTechnology(ai, s);
                case "repair":     return s.Life < s.MaxLife && s.Population >= CivicUpgrades.CostRepair + buffer && CivicUpgrades.Repair(s);
                case "conscript":  return s.Population >= CivicUpgrades.CostConscript  + buffer && CivicUpgrades.Conscript(game, ai, s) != null;
                default: return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  MINING
        // ═══════════════════════════════════════════════════════════════════════════
        private void HandleMining(Player aiPlayer)
        {
            game.UpdateSupplyLines();

            foreach (var s in aiPlayer.Structures.ToList())
            {
                List<LandUnit> barracks = (s as Base)?.Barracks ?? (s as City)?.Barracks;
                if (barracks == null) continue;
                foreach (var m in barracks.OfType<Miner>().ToList())
                {
                    var pos = FindAdjacentEmptyLand(s.Position);
                    if (pos == null) break;
                    barracks.Remove(m);
                    m.Position = pos.Value;
                    game.Map.GetTile(pos.Value).Units.Add(m);
                }
            }

            foreach (var miner in aiPlayer.Units.OfType<Miner>().ToList())
            {
                if (miner.Position.X < 0 || miner.MovementPoints <= 0) continue;
                var tile = game.Map.GetTile(miner.Position);
                if (ResourceRegistry.IsMineable(tile.Resource) && tile.Structure == null)
                {
                    game.BuildMine(miner, aiPlayer);
                    continue;
                }
                var target = FindNearestUnminedResource(miner.Position);
                if (target != null) MoveToward(miner, target.Value, aiPlayer);
            }

            foreach (var sapper in aiPlayer.Units.OfType<Sapper>().ToList())
            {
                if (sapper.IsDisruptingSupply || sapper.IsBuildingBase || sapper.IsBuildingBridge) continue;
                if (sapper.Position.X < 0 || sapper.MovementPoints <= 0) continue;

                bool onLine = false;
                TilePosition? nearestLineTile = null;
                int bestDist = int.MaxValue;
                foreach (var p in game.Players)
                {
                    if (p.PlayerId == aiPlayer.PlayerId) continue;
                    foreach (var m in p.Structures.OfType<Mine>())
                    {
                        if (m.SupplyPath == null) continue;
                        foreach (var pt in m.SupplyPath)
                        {
                            if (pt.Equals(sapper.Position)) onLine = true;
                            int d = Math.Abs(pt.X - sapper.Position.X) + Math.Abs(pt.Y - sapper.Position.Y);
                            if (d < bestDist) { bestDist = d; nearestLineTile = pt; }
                        }
                    }
                }
                if (onLine) { sapper.IsDisruptingSupply = true; sapper.MovementPoints = 0; }
                else if (nearestLineTile != null) MoveToward(sapper, nearestLineTile.Value, aiPlayer);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  PRODUCTION  — scales with empire size, differentiated by personality
        // ═══════════════════════════════════════════════════════════════════════════
        private void HandleProduction(Player aiPlayer)
        {
            foreach (var structure in aiPlayer.Structures)
            {
                if (structure is Base baseStructure)
                {
                    if (baseStructure.ProductionQueue.Count < GetProductionQueueSize())
                    {
                        var order = DecideWhatToBuild(baseStructure, aiPlayer);
                        if (order != null && CanEnqueueOrder(order, baseStructure, aiPlayer))
                            EnqueueOrder(order, baseStructure, aiPlayer);
                    }
                }
                else if (structure is City city)
                {
                    if (city.ProductionQueue.Count < GetProductionQueueSize())
                    {
                        var order = DecideWhatToBuild(city, aiPlayer);
                        if (order != null && CanEnqueueOrder(order, city, aiPlayer))
                            EnqueueOrder(order, city, aiPlayer);
                    }
                }
            }
        }

        private bool CanEnqueueOrder(UnitProductionOrder order, Structure s, Player ai)
        {
            int popCost = UnitProductionOrder.PopulationCost(order.UnitType);
            if (popCost > 0 && s.Population - popCost < 1) return false;
            return true;
        }

        private void EnqueueOrder(UnitProductionOrder order, Structure s, Player ai)
        {
            foreach (var kv in order.Cost)
                ai.AddResource(kv.Key, -kv.Value);
            int popCost = UnitProductionOrder.PopulationCost(order.UnitType);
            if (popCost > 0) s.Population -= popCost;
            if (s is Base b) b.ProductionQueue.Enqueue(order);
            else if (s is City c) c.ProductionQueue.Enqueue(order);
        }

        private int GetProductionQueueSize() => playstyle switch
        {
            AIPlaystyle.Buildup    => 3,
            AIPlaystyle.Aggressive => 1,
            _ => 2
        };

        private UnitProductionOrder DecideWhatToBuild(Structure structure, Player aiPlayer)
        {
            bool canProduceNaval = structure is Base b && b.CanProduceNaval;

            bool CanAfford(Type t) => UnitProductionOrder.GetCost(t).All(kv => aiPlayer.GetResource(kv.Key) >= kv.Value);

            // Count existing units
            int armies    = aiPlayer.Units.Count(u => u is Army);
            int tanks     = aiPlayer.Units.Count(u => u is Tank);
            int arty      = aiPlayer.Units.Count(u => u is Artillery);
            int aa        = aiPlayer.Units.Count(u => u is AntiAircraft);
            int fighters  = aiPlayer.Units.Count(u => u is Fighter);
            int bombers   = aiPlayer.Units.Count(u => u is Bomber);
            int carriers  = aiPlayer.Units.Count(u => u is Carrier);
            int battleships = aiPlayer.Units.Count(u => u is Battleship);
            int destroyers = aiPlayer.Units.Count(u => u is Destroyer);
            int subs      = aiPlayer.Units.Count(u => u is Submarine);
            int patrols   = aiPlayer.Units.Count(u => u is PatrolBoat);
            int transports = aiPlayer.Units.Count(u => u is Transport);
            int tankers   = aiPlayer.Units.Count(u => u is Tanker);
            int spies     = aiPlayer.Units.Count(u => u is Spy);
            int sappers   = aiPlayer.Units.Count(u => u is Sapper);

            // Scale caps with empire size — more structures → bigger armies
            int structures = aiPlayer.Structures.Count(s => s is Base || s is City);
            int scaledBase = Math.Max(1, structures);

            int mineCount  = aiPlayer.Structures.Count(s => s is Mine);
            int minerCount = aiPlayer.Units.Count(u => u is Miner)
                           + aiPlayer.Structures.OfType<Base>().Sum(bx => bx.Barracks.Count(x => x is Miner))
                           + aiPlayer.Structures.OfType<City>().Sum(c => c.Barracks.Count(x => x is Miner));

            // Ensure a spy is present for all personalities
            if (spies == 0 && CanAfford(typeof(Spy)))
                return new UnitProductionOrder(typeof(Spy), "Spy");

            switch (playstyle)
            {
                // ── Aggressive: heavy offensive, scales hard with empire ──────────
                case AIPlaystyle.Aggressive:
                    if (tanks    < scaledBase * 3 + 2 && CanAfford(typeof(Tank)))     return new UnitProductionOrder(typeof(Tank), "Tank");
                    if (armies   < scaledBase * 4 + 4 && CanAfford(typeof(Army)))     return new UnitProductionOrder(typeof(Army), "Army");
                    if (bombers  < scaledBase + 2     && CanAfford(typeof(Bomber)))   return new UnitProductionOrder(typeof(Bomber), "Bomber");
                    if (arty     < scaledBase + 1     && CanAfford(typeof(Artillery)))return new UnitProductionOrder(typeof(Artillery), "Artillery");
                    if (sappers  == 0 && CanAfford(typeof(Sapper)))                   return new UnitProductionOrder(typeof(Sapper), "Sapper");
                    // Always fill production with armies
                    if (CanAfford(typeof(Army)))  return new UnitProductionOrder(typeof(Army), "Army");
                    break;

                // ── Defensive: balanced force, prioritise AA & artillery ─────────
                case AIPlaystyle.Defensive:
                    if (aa       < scaledBase * 2     && CanAfford(typeof(AntiAircraft))) return new UnitProductionOrder(typeof(AntiAircraft), "AntiAircraft");
                    if (arty     < scaledBase * 2     && CanAfford(typeof(Artillery)))    return new UnitProductionOrder(typeof(Artillery), "Artillery");
                    if (fighters < scaledBase * 2     && CanAfford(typeof(Fighter)))      return new UnitProductionOrder(typeof(Fighter), "Fighter");
                    if (armies   < scaledBase * 3 + 3 && CanAfford(typeof(Army)))         return new UnitProductionOrder(typeof(Army), "Army");
                    if (tanks    < scaledBase + 1     && CanAfford(typeof(Tank)))         return new UnitProductionOrder(typeof(Tank), "Tank");
                    // Miners for resource income
                    if (mineCount + minerCount < scaledBase * 2 && CanAfford(typeof(Miner))) return new UnitProductionOrder(typeof(Miner), "Miner");
                    break;

                // ── Naval: sea dominance first, then land follow-up ───────────────
                case AIPlaystyle.Naval:
                    if (canProduceNaval)
                    {
                        if (carriers    < scaledBase     && CanAfford(typeof(Carrier)))     return new UnitProductionOrder(typeof(Carrier), "Carrier");
                        if (battleships < scaledBase + 1 && CanAfford(typeof(Battleship)))  return new UnitProductionOrder(typeof(Battleship), "Battleship");
                        if (destroyers  < scaledBase * 2 && CanAfford(typeof(Destroyer)))   return new UnitProductionOrder(typeof(Destroyer), "Destroyer");
                        if (subs        < scaledBase + 1 && CanAfford(typeof(Submarine)))   return new UnitProductionOrder(typeof(Submarine), "Submarine");
                        if (transports  < scaledBase     && CanAfford(typeof(Transport)))   return new UnitProductionOrder(typeof(Transport), "Transport");
                    }
                    if (armies < scaledBase * 2 + 2 && CanAfford(typeof(Army)))             return new UnitProductionOrder(typeof(Army), "Army");
                    if (tanks  < scaledBase          && CanAfford(typeof(Tank)))             return new UnitProductionOrder(typeof(Tank), "Tank");
                    break;

                // ── Aerial: air fleet first, ground support ───────────────────────
                case AIPlaystyle.Aerial:
                    if (fighters < scaledBase * 3     && CanAfford(typeof(Fighter)))    return new UnitProductionOrder(typeof(Fighter), "Fighter");
                    if (bombers  < scaledBase * 2     && CanAfford(typeof(Bomber)))     return new UnitProductionOrder(typeof(Bomber), "Bomber");
                    if (tankers  < scaledBase         && CanAfford(typeof(Tanker)))     return new UnitProductionOrder(typeof(Tanker), "Tanker");
                    if (canProduceNaval && carriers < 1 && CanAfford(typeof(Carrier))) return new UnitProductionOrder(typeof(Carrier), "Carrier");
                    if (armies  < scaledBase * 2 + 2  && CanAfford(typeof(Army)))      return new UnitProductionOrder(typeof(Army), "Army");
                    if (aa      < scaledBase           && CanAfford(typeof(AntiAircraft))) return new UnitProductionOrder(typeof(AntiAircraft), "AntiAircraft");
                    break;

                // ── Buildup: economy first, then army ────────────────────────────
                case AIPlaystyle.Buildup:
                    if (sappers < 2 && CanAfford(typeof(Sapper)))                         return new UnitProductionOrder(typeof(Sapper), "Sapper");
                    if (mineCount + minerCount < scaledBase * 3 && CanAfford(typeof(Miner))) return new UnitProductionOrder(typeof(Miner), "Miner");
                    if (armies  < scaledBase * 2 + 2   && CanAfford(typeof(Army)))        return new UnitProductionOrder(typeof(Army), "Army");
                    if (tanks   < scaledBase + 1        && CanAfford(typeof(Tank)))        return new UnitProductionOrder(typeof(Tank), "Tank");
                    if (fighters < scaledBase            && CanAfford(typeof(Fighter)))    return new UnitProductionOrder(typeof(Fighter), "Fighter");
                    break;
            }

            // ── Balanced fallback and shared expansion logic ────────────────────
            if (mineCount + minerCount < scaledBase * 2 && CanAfford(typeof(Miner)))
                return new UnitProductionOrder(typeof(Miner), "Miner");

            int baseCount = aiPlayer.Structures.Count(s => s is Base);
            if (baseCount < GetExpansionBaseTarget() && sappers == 0 && CanAfford(typeof(Sapper)))
                return new UnitProductionOrder(typeof(Sapper), "Sapper");

            // Balanced army composition
            int totalLand = armies + tanks + arty + aa;
            if (totalLand < scaledBase * 5 + 4)
            {
                if (armies < scaledBase * 3 + 3 && CanAfford(typeof(Army)))      return new UnitProductionOrder(typeof(Army), "Army");
                if (tanks  < scaledBase + 2      && CanAfford(typeof(Tank)))      return new UnitProductionOrder(typeof(Tank), "Tank");
                if (arty   < scaledBase + 1      && CanAfford(typeof(Artillery))) return new UnitProductionOrder(typeof(Artillery), "Artillery");
            }

            int totalAir = fighters + bombers + tankers;
            if (totalAir < scaledBase * 3 + 2)
            {
                if (fighters < scaledBase + 2 && CanAfford(typeof(Fighter))) return new UnitProductionOrder(typeof(Fighter), "Fighter");
                if (bombers  < scaledBase + 1 && CanAfford(typeof(Bomber)))  return new UnitProductionOrder(typeof(Bomber), "Bomber");
            }

            if (canProduceNaval && destroyers < scaledBase + 1 && CanAfford(typeof(Destroyer)))
                return new UnitProductionOrder(typeof(Destroyer), "Destroyer");
            if (canProduceNaval && patrols < scaledBase && CanAfford(typeof(PatrolBoat)))
                return new UnitProductionOrder(typeof(PatrolBoat), "PatrolBoat");

            if (CanAfford(typeof(Army))) return new UnitProductionOrder(typeof(Army), "Army");
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  UNIT HANDLING  — personality-driven role assignment
        // ═══════════════════════════════════════════════════════════════════════════
        private void HandleUnits(Player aiPlayer)
        {
            var weights = GetStrategyWeights();
            var units   = aiPlayer.Units.ToList();

            bool homeUnderThreat = IsHomeThreatened(aiPlayer);
            int  defendersNeeded = GetDefendersNeeded(aiPlayer, weights, homeUnderThreat);
            int  assignedDefenders = 0;

            foreach (var unit in units)
            {
                if (unit.MovementPoints <= 0) continue;
                if (unit is Miner || unit is Sapper) continue; // handled by HandleMining / HandleSappers

                if (unit is AirUnit air)   { HandleAirUnit(air, aiPlayer, weights);       continue; }
                if (unit is SeaUnit sea)   { HandleNavalUnit(sea, aiPlayer, weights);      continue; }
                if (unit is Spy spy)       { HandleSpy(spy, aiPlayer);                    continue; }

                // Assign land units as defenders if still needed and close to home
                if (assignedDefenders < defendersNeeded && IsNearFriendlyStructure(unit, aiPlayer, 5))
                {
                    assignedDefenders++;
                    HandleDefender(unit, aiPlayer, homeUnderThreat);
                    continue;
                }

                HandleLandUnit(unit, aiPlayer, weights);
            }
        }

        // ─── Land unit: personality-driven priority list ──────────────────────────
        private void HandleLandUnit(Unit unit, Player aiPlayer, StrategyWeights w)
        {
            // Recapture own lost structures — universal high priority
            var lostTarget = FindLostStructureToRecapture(unit, aiPlayer);
            if (lostTarget != null && ShouldRecaptureStructure(aiPlayer, lostTarget))
            {
                MoveToward(unit, lostTarget.Position, aiPlayer);
                return;
            }

            switch (playstyle)
            {
                // ── Aggressive: go for the jugular ────────────────────────────────
                case AIPlaystyle.Aggressive:
                {
                    var target = FindBestAssaultTarget(unit, aiPlayer);
                    if (target.HasValue) { MoveToward(unit, target.Value, aiPlayer); return; }

                    var enemy = FindNearbyEnemy(unit, aiPlayer);
                    if (enemy != null)
                    {
                        if (IsAdjacent(unit.Position, enemy.Position)) ExecuteCombat(unit, enemy, aiPlayer);
                        else MoveToward(unit, enemy.Position, aiPlayer);
                        return;
                    }
                    ExploreForward(unit, aiPlayer);
                    break;
                }

                // ── Defensive: defend home, only counter-attack if pressed ────────
                case AIPlaystyle.Defensive:
                {
                    var adj = FindAdjacentEnemy(unit, aiPlayer);
                    if (adj != null) { ExecuteCombat(unit, adj, aiPlayer); return; }

                    // Counter-attack if aggression is high toward this enemy
                    var closeEnemy = FindNearbyEnemy(unit, aiPlayer);
                    if (closeEnemy != null)
                    {
                        int agg = aiPlayer.GetAggressionTowardsPlayer(closeEnemy.OwnerId);
                        if (agg >= 2 && unit.Life >= closeEnemy.Life)
                        {
                            MoveToward(unit, closeEnemy.Position, aiPlayer);
                            return;
                        }
                    }
                    ExploreNearHome(unit, aiPlayer);
                    break;
                }

                // ── Buildup: secure resources and expand before fighting ──────────
                case AIPlaystyle.Buildup:
                {
                    // Move toward unclaimed resource tiles for future mining
                    var resTarget = FindNearestUnclaimedResourceTile(unit, aiPlayer);
                    if (resTarget.HasValue) { MoveToward(unit, resTarget.Value, aiPlayer); return; }

                    var enemy = FindNearbyEnemy(unit, aiPlayer);
                    if (enemy != null && ShouldEngageEnemy(unit, enemy, aiPlayer))
                    {
                        if (IsAdjacent(unit.Position, enemy.Position)) ExecuteCombat(unit, enemy, aiPlayer);
                        else MoveToward(unit, enemy.Position, aiPlayer);
                        return;
                    }
                    ExploreOrDefend(unit, aiPlayer);
                    break;
                }

                // ── Balanced / Naval (land) / Aerial (land): balanced priority ────
                default:
                {
                    var target = FindBestAssaultTarget(unit, aiPlayer);
                    if (target.HasValue)
                    {
                        var targetTile = game.Map.GetTile(target.Value);
                        bool safe = targetTile.Units.Count(u => u.OwnerId != aiPlayer.PlayerId) <= 2;
                        if (safe || playstyle == AIPlaystyle.Balanced && aiPlayer.GetAggressionTowardsPlayer(targetTile.OwnerId) > 0)
                        {
                            MoveToward(unit, target.Value, aiPlayer);
                            return;
                        }
                    }

                    var enemy = FindNearbyEnemy(unit, aiPlayer);
                    if (enemy != null && ShouldEngageEnemy(unit, enemy, aiPlayer))
                    {
                        if (IsAdjacent(unit.Position, enemy.Position)) ExecuteCombat(unit, enemy, aiPlayer);
                        else MoveToward(unit, enemy.Position, aiPlayer);
                        return;
                    }
                    ExploreOrDefend(unit, aiPlayer);
                    break;
                }
            }
        }

        // ─── Defender: garrison home structures, respond to nearby threats ────────
        private void HandleDefender(Unit unit, Player aiPlayer, bool homeUnderThreat)
        {
            // Always attack adjacent enemies
            var adj = FindAdjacentEnemy(unit, aiPlayer);
            if (adj != null) { ExecuteCombat(unit, adj, aiPlayer); return; }

            if (homeUnderThreat)
            {
                var threatened = FindNearestThreatenedStructure(aiPlayer);
                if (threatened != null) { MoveToward(unit, threatened.Position, aiPlayer); return; }
            }

            // Patrol near nearest home structure
            var home = FindNearestFriendlyStructure(unit, aiPlayer);
            if (home != null)
            {
                int dist = Math.Abs(unit.Position.X - home.Position.X) + Math.Abs(unit.Position.Y - home.Position.Y);
                if (dist > 3) MoveToward(unit, home.Position, aiPlayer);
                else          RandomMove(unit);
            }
        }

        // ─── Air units: fuel management, role-specific tasks ─────────────────────
        private void HandleAirUnit(AirUnit unit, Player aiPlayer, StrategyWeights w)
        {
            // RTB when fuel runs low
            if (unit.Fuel <= unit.MaxFuel * 0.35f)
            {
                ReturnToBase(unit, aiPlayer);
                return;
            }

            if (unit is Tanker) { Patrol(unit); return; }

            if (unit is Bomber)
            {
                // Bombers prioritise visible enemy structures by value
                var target = FindBestStructureTarget(unit, aiPlayer);
                if (target.HasValue) { MoveToward(unit, target.Value, aiPlayer); return; }
            }
            else if (unit is Fighter)
            {
                // Fighters intercept enemy air first, then enemy ground
                var enemyAir = FindNearbyEnemyAirUnit(unit, aiPlayer);
                if (enemyAir != null)
                {
                    if (IsAdjacent(unit.Position, enemyAir.Position)) ExecuteCombat(unit, enemyAir, aiPlayer);
                    else MoveToward(unit, enemyAir.Position, aiPlayer);
                    return;
                }

                // Support ground assault if war weight is high
                if (w.War >= 0.6f)
                {
                    var groundTarget = FindBestAssaultTarget(unit, aiPlayer);
                    if (groundTarget.HasValue) { MoveToward(unit, groundTarget.Value, aiPlayer); return; }
                }
            }

            Patrol(unit);
        }

        // ─── Naval units ─────────────────────────────────────────────────────────
        private void HandleNavalUnit(SeaUnit unit, Player aiPlayer, StrategyWeights w)
        {
            // Attack adjacent enemy ships immediately
            var adj = FindAdjacentEnemy(unit, aiPlayer);
            if (adj != null && adj is SeaUnit) { ExecuteCombat(unit, adj, aiPlayer); return; }

            // Seek enemy ships if sufficiently warlike
            var enemyShip = FindNearbyEnemyNavalUnit(unit, aiPlayer);
            if (enemyShip != null && w.War >= 0.5f)
            {
                if (IsAdjacent(unit.Position, enemyShip.Position)) ExecuteCombat(unit, enemyShip, aiPlayer);
                else MoveToward(unit, enemyShip.Position, aiPlayer);
                return;
            }

            // Transports: ferry armies to enemy coast
            if (unit is Transport) { HandleTransport(unit, aiPlayer); return; }

            // Assault personalities attack coastal structures
            if (w.Assault >= 0.6f)
            {
                var coastal = FindNearestCoastalEnemyStructure(unit, aiPlayer);
                if (coastal.HasValue) { MoveToward(unit, coastal.Value, aiPlayer); return; }
            }

            Patrol(unit);
        }

        private void HandleTransport(SeaUnit transport, Player aiPlayer)
        {
            // If carrying armies, move toward nearest enemy coast
            var coastal = FindNearestCoastalEnemyStructure(transport, aiPlayer);
            if (coastal.HasValue) { MoveToward(transport, coastal.Value, aiPlayer); return; }
            Patrol(transport);
        }

        // ─── Spy ──────────────────────────────────────────────────────────────────
        private void HandleSpy(Spy spy, Player aiPlayer)
        {
            if (spy.IsRevealed)
            {
                var home = FindNearestFriendlyStructure(spy, aiPlayer);
                if (home != null) MoveToward(spy, home.Position, aiPlayer);
                return;
            }
            // Infiltrate enemy territory
            var target = FindNearbyEnemyStructure(spy, aiPlayer);
            if (target != null) MoveToward(spy, target.Position, aiPlayer);
            else ExploreOrDefend(spy, aiPlayer);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  SAPPERS — personality-driven base expansion
        // ═══════════════════════════════════════════════════════════════════════════
        private void HandleSappers(Player aiPlayer)
        {
            var sappers = aiPlayer.Units.OfType<Sapper>().ToList();
            int baseTarget = GetExpansionBaseTarget();
            int currentBases = aiPlayer.Structures.Count(s => s is Base);

            foreach (var sapper in sappers)
            {
                if (sapper.IsBuildingBase || sapper.IsBuildingBridge) continue;
                if (sapper.MovementPoints <= 0) continue;

                // Priority 1: Build a base if below expansion target and location is safe
                if (currentBases < baseTarget && sapper.CanBuildBaseAt(sapper.Position, game.Map) && IsSafeLocation(sapper.Position, aiPlayer))
                {
                    sapper.StartBuildingBase(sapper.Position);
                    messageCallback?.Invoke($"🏗️ {aiPlayer.Name}'s Sapper began building a base.", MessageType.Info);
                    currentBases++;
                    continue;
                }

                // Priority 2: Navigate toward a good base location
                if (currentBases < baseTarget)
                {
                    var loc = FindGoodBaseLocation(aiPlayer, sapper);
                    if (loc.X != -1) { MoveToward(sapper, loc, aiPlayer); continue; }
                }

                // Priority 3: Build bridges for connectivity
                var bridge = FindBridgeBuildLocation(aiPlayer, sapper);
                if (bridge.X != -1)
                {
                    if (sapper.CanBuildBridgeAt(bridge, game.Map))
                    {
                        sapper.StartBuildingBridge(bridge);
                        messageCallback?.Invoke($"🌉 {aiPlayer.Name}'s Sapper began building a bridge.", MessageType.Info);
                    }
                    else MoveToward(sapper, bridge, aiPlayer);
                    continue;
                }

                // Default: move toward center of territory
                var center = FindCenterOfTerritory(aiPlayer);
                if (center.X != -1) MoveToward(sapper, center, aiPlayer);
            }
        }

        private int GetExpansionBaseTarget() => playstyle switch
        {
            AIPlaystyle.Aggressive => 5,
            AIPlaystyle.Buildup    => 6,
            AIPlaystyle.Defensive  => 2,
            AIPlaystyle.Naval      => 4,
            AIPlaystyle.Aerial     => 4,
            _                      => 3, // Balanced
        };

        private bool IsSafeLocation(TilePosition pos, Player aiPlayer)
        {
            for (int dx = -4; dx <= 4; dx++)
                for (int dy = -4; dy <= 4; dy++)
                {
                    var check = new TilePosition(pos.X + dx, pos.Y + dy);
                    if (!game.Map.IsValidPosition(check)) continue;
                    var tile = game.Map.GetTile(check);
                    if (tile.Units.Any(u => u.OwnerId != aiPlayer.PlayerId && !(u is Spy spy2 && !spy2.IsRevealed)))
                        return false;
                }
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  TARGETING  — scored best assault target
        // ═══════════════════════════════════════════════════════════════════════════

        // Returns the highest-value visible enemy structure/tile to assault.
        private TilePosition? FindBestAssaultTarget(Unit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius();
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);

            TilePosition? best = null;
            float bestScore = 0f;

            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                if (tile.Structure == null || tile.Structure.OwnerId == aiPlayer.PlayerId) continue;

                float score = ScoreAssaultTarget(tile.Structure, unit, aiPlayer);
                if (score > bestScore) { bestScore = score; best = tile.Structure.Position; }
            }

            return best;
        }

        private float ScoreAssaultTarget(Structure target, Unit unit, Player aiPlayer)
        {
            // Base value by structure type
            float score = target switch
            {
                City _ => 100f,
                Base _ => 80f,
                Mine _ => 30f,
                _      => 20f,
            };

            // Personality modifier
            score *= playstyle switch
            {
                AIPlaystyle.Aggressive => 1.4f,
                AIPlaystyle.Aerial     => 1.3f,
                AIPlaystyle.Naval      => 1.2f,
                AIPlaystyle.Buildup    => 0.5f,
                AIPlaystyle.Defensive  => 0.3f,
                _                      => 1.0f,
            };

            // Aggression bonus: prior losses against this owner increase motivation
            score += aiPlayer.GetAggressionTowardsPlayer(target.OwnerId) * 10f;

            // Distance penalty
            int dist = Math.Abs(unit.Position.X - target.Position.X) + Math.Abs(unit.Position.Y - target.Position.Y);
            score -= dist * 3f;

            // Defender risk penalty for cautious AIs
            if (playstyle != AIPlaystyle.Aggressive)
            {
                int defenders = game.Map.GetTile(target.Position).Units.Count(u => u.OwnerId == target.OwnerId);
                score -= defenders * 12f;
            }

            // Bonus for HP-damaged structures (easier to take)
            if (target.Life < target.MaxLife)
                score += (1f - (float)target.Life / target.MaxLife) * 20f;

            return score;
        }

        // Picks the best visible enemy structure for air strikes
        private TilePosition? FindBestStructureTarget(AirUnit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius() + 3; // bombers range further
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);

            TilePosition? best = null;
            float bestScore = 0f;

            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                if (tile.Structure == null || tile.Structure.OwnerId == aiPlayer.PlayerId) continue;

                float score = ScoreAssaultTarget(tile.Structure, unit, aiPlayer);
                if (score > bestScore) { bestScore = score; best = tile.Structure.Position; }
            }

            return best;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  THREAT / GARRISON HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        private bool IsHomeThreatened(Player aiPlayer)
        {
            int threatRadius = playstyle == AIPlaystyle.Defensive ? 6 : 4;
            foreach (var s in aiPlayer.Structures)
            {
                if (!(s is Base || s is City)) continue;
                for (int dx = -threatRadius; dx <= threatRadius; dx++)
                    for (int dy = -threatRadius; dy <= threatRadius; dy++)
                    {
                        var pos = new TilePosition(s.Position.X + dx, s.Position.Y + dy);
                        if (!game.Map.IsValidPosition(pos)) continue;
                        var tile = game.Map.GetTile(pos);
                        if (tile.Units.Any(u => u.OwnerId != aiPlayer.PlayerId && !(u is Spy sp && !sp.IsRevealed)))
                            return true;
                    }
            }
            return false;
        }

        private int GetDefendersNeeded(Player aiPlayer, StrategyWeights w, bool homeUnderThreat)
        {
            int structures = aiPlayer.Structures.Count(s => s is Base || s is City);
            float base_defenders = structures * w.Defense * 2f;
            if (homeUnderThreat) base_defenders *= 1.5f;
            return (int)Math.Ceiling(base_defenders);
        }

        private bool IsNearFriendlyStructure(Unit unit, Player aiPlayer, int radius = 4)
        {
            foreach (var s in aiPlayer.Structures)
            {
                if (!(s is Base || s is City)) continue;
                int dist = Math.Abs(unit.Position.X - s.Position.X) + Math.Abs(unit.Position.Y - s.Position.Y);
                if (dist <= radius) return true;
            }
            return false;
        }

        private Structure FindNearestFriendlyStructure(Unit unit, Player aiPlayer)
        {
            Structure nearest = null;
            int minDist = int.MaxValue;
            foreach (var s in aiPlayer.Structures)
            {
                if (!(s is Base || s is City)) continue;
                int d = Math.Abs(unit.Position.X - s.Position.X) + Math.Abs(unit.Position.Y - s.Position.Y);
                if (d < minDist) { minDist = d; nearest = s; }
            }
            return nearest;
        }

        private Structure FindNearestThreatenedStructure(Player aiPlayer)
        {
            int threatRadius = 6;
            Structure nearest = null;
            int minThreat = int.MaxValue;

            foreach (var s in aiPlayer.Structures)
            {
                if (!(s is Base || s is City)) continue;
                for (int dx = -threatRadius; dx <= threatRadius; dx++)
                    for (int dy = -threatRadius; dy <= threatRadius; dy++)
                    {
                        var pos = new TilePosition(s.Position.X + dx, s.Position.Y + dy);
                        if (!game.Map.IsValidPosition(pos)) continue;
                        var tile = game.Map.GetTile(pos);
                        if (tile.Units.Any(u => u.OwnerId != aiPlayer.PlayerId && !(u is Spy sp && !sp.IsRevealed)))
                        {
                            int d = Math.Abs(dx) + Math.Abs(dy);
                            if (d < minThreat) { minThreat = d; nearest = s; }
                        }
                    }
            }
            return nearest;
        }

        private Unit FindAdjacentEnemy(Unit unit, Player aiPlayer)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var pos = new TilePosition(unit.Position.X + dx, unit.Position.Y + dy);
                    if (!game.Map.IsValidPosition(pos)) continue;
                    var tile = game.Map.GetTile(pos);
                    var enemy = tile.Units.FirstOrDefault(u => u.OwnerId != aiPlayer.PlayerId && !(u is Spy sp && !sp.IsRevealed));
                    if (enemy != null) return enemy;
                }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  EXPLORATION VARIANTS
        // ═══════════════════════════════════════════════════════════════════════════

        // Aggressive/Aerial: head toward the enemy side of the map
        private void ExploreForward(Unit unit, Player aiPlayer)
        {
            // Find the average enemy position as a general direction
            var enemies = game.Players.Where(p => p.PlayerId != aiPlayer.PlayerId && !p.HasBeenEliminated)
                              .SelectMany(p => p.Structures.Select(s => s.Position))
                              .ToList();
            if (enemies.Count > 0)
            {
                int avgX = (int)enemies.Average(e => e.X);
                int avgY = (int)enemies.Average(e => e.Y);
                MoveToward(unit, new TilePosition(avgX, avgY), aiPlayer);
                return;
            }
            ExploreOrDefend(unit, aiPlayer);
        }

        // Defensive: only explore within home territory bounds
        private void ExploreNearHome(Unit unit, Player aiPlayer)
        {
            var home = FindNearestFriendlyStructure(unit, aiPlayer);
            int homeRadius = 8;

            List<TilePosition> targets = new List<TilePosition>();
            int cx = home?.Position.X ?? unit.Position.X;
            int cy = home?.Position.Y ?? unit.Position.Y;

            for (int x = cx - homeRadius; x <= cx + homeRadius; x++)
                for (int y = cy - homeRadius; y <= cy + homeRadius; y++)
                {
                    var pos = new TilePosition(x, y);
                    if (game.Map.IsValidPosition(pos) &&
                        (!aiPlayer.FogOfWar.ContainsKey(pos) || aiPlayer.FogOfWar[pos] == VisibilityLevel.Explored))
                        targets.Add(pos);
                }

            if (targets.Count > 0) MoveToward(unit, targets[rand.Next(targets.Count)], aiPlayer);
            else RandomMove(unit);
        }

        // Buildup: move toward tiles with minable resources for future mines
        private TilePosition? FindNearestUnclaimedResourceTile(Unit unit, Player aiPlayer)
        {
            TilePosition? best = null;
            int bestD = int.MaxValue;
            for (int x = 0; x < game.Map.Width; x++)
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    var t = game.Map.GetTile(pos);
                    if (!ResourceRegistry.IsMineable(t.Resource)) continue;
                    if (t.Structure != null) continue;
                    if (!IsVisible(pos, aiPlayer)) continue;
                    // Only claim unclaimed or own territory
                    if (t.OwnerId != -1 && t.OwnerId != aiPlayer.PlayerId) continue;
                    int d = Math.Abs(x - unit.Position.X) + Math.Abs(y - unit.Position.Y);
                    if (d < bestD) { bestD = d; best = pos; }
                }
            return best;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  NAVAL / AIR SPECIFIC FINDERS
        // ═══════════════════════════════════════════════════════════════════════════

        private Unit FindNearbyEnemyNavalUnit(SeaUnit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius();
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);
            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                var enemy = tile.Units.FirstOrDefault(u => u.OwnerId != aiPlayer.PlayerId && u is SeaUnit &&
                                                            !(u is Spy sp && !sp.IsRevealed));
                if (enemy != null) return enemy;
            }
            return null;
        }

        private Unit FindNearbyEnemyAirUnit(AirUnit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius();
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);
            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                var enemy = tile.Units.FirstOrDefault(u => u.OwnerId != aiPlayer.PlayerId && u is AirUnit &&
                                                            !(u is Spy sp && !sp.IsRevealed));
                if (enemy != null) return enemy;
            }
            return null;
        }

        private TilePosition? FindNearestCoastalEnemyStructure(Unit unit, Player aiPlayer)
        {
            TilePosition? best = null;
            int bestD = int.MaxValue;
            foreach (var player in game.Players)
            {
                if (player.PlayerId == aiPlayer.PlayerId) continue;
                foreach (var s in player.Structures)
                {
                    if (!IsVisible(s.Position, aiPlayer)) continue;
                    // Check if this structure has adjacent coastal water
                    bool coastal = false;
                    for (int dx2 = -1; dx2 <= 1 && !coastal; dx2++)
                        for (int dy2 = -1; dy2 <= 1 && !coastal; dy2++)
                        {
                            var np = new TilePosition(s.Position.X + dx2, s.Position.Y + dy2);
                            if (!game.Map.IsValidPosition(np)) continue;
                            var nt = game.Map.GetTile(np);
                            if (nt.Terrain == TerrainType.Ocean || nt.Terrain == TerrainType.CoastalWater)
                                coastal = true;
                        }
                    if (!coastal) continue;
                    int d = Math.Abs(unit.Position.X - s.Position.X) + Math.Abs(unit.Position.Y - s.Position.Y);
                    if (d < bestD) { bestD = d; best = s.Position; }
                }
            }
            return best;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  EXISTING HELPERS  (mostly unchanged)
        // ═══════════════════════════════════════════════════════════════════════════

        private Structure FindLostStructureToRecapture(Unit unit, Player aiPlayer)
        {
            if (aiPlayer.LostStructures.Count == 0) return null;
            Structure nearest = null;
            int nearestDist = int.MaxValue;
            foreach (var lost in aiPlayer.LostStructures)
            {
                if (!game.Map.IsValidPosition(lost.Position)) continue;
                int d = Math.Abs(unit.Position.X - lost.Position.X) + Math.Abs(unit.Position.Y - lost.Position.Y);
                if (d < nearestDist) { nearestDist = d; nearest = lost; }
            }
            return nearest;
        }

        private bool ShouldRecaptureStructure(Player aiPlayer, Structure lost)
        {
            int remaining = aiPlayer.Structures.Count;
            return playstyle switch
            {
                AIPlaystyle.Aggressive => true,
                AIPlaystyle.Balanced   => true,
                AIPlaystyle.Defensive  => remaining <= 1 || true, // always try
                _                      => remaining == 0,
            };
        }

        private Structure FindNearbyEnemyStructure(Unit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius();
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);
            Structure best = null;
            float bestScore = 0f;
            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                if (tile.Structure == null || tile.Structure.OwnerId == aiPlayer.PlayerId) continue;
                float score = ScoreAssaultTarget(tile.Structure, unit, aiPlayer);
                if (score > bestScore) { bestScore = score; best = tile.Structure; }
            }
            return best;
        }

        private bool ShouldCaptureStructure(Unit unit, Structure target, Player aiPlayer)
        {
            var defenders = game.Map.GetTile(target.Position).Units
                                .Where(u => u.OwnerId == target.OwnerId).ToList();
            int aggressionBonus = aiPlayer.GetAggressionTowardsPlayer(target.OwnerId);

            return playstyle switch
            {
                AIPlaystyle.Aggressive => true,
                AIPlaystyle.Balanced   => aggressionBonus > 0 ? unit.Life >= defenders.Sum(d => d.Life) * 0.7f
                                                              : unit.Life >= defenders.Sum(d => d.Life) * 0.9f,
                AIPlaystyle.Defensive  => unit.Life >= defenders.Sum(d => d.Life) * 1.5f,
                _                      => unit.Life >= defenders.Sum(d => d.Life),
            };
        }

        private bool ShouldEngageEnemy(Unit unit, Unit enemy, Player aiPlayer)
        {
            int agg = aiPlayer.GetAggressionTowardsPlayer(enemy.OwnerId);
            return playstyle switch
            {
                AIPlaystyle.Aggressive => true,
                AIPlaystyle.Balanced   => agg > 0 ? unit.Life >= enemy.Life * 0.7f : unit.Life >= enemy.Life * 0.8f,
                AIPlaystyle.Defensive  => unit.Life > enemy.Life * 1.2f,
                AIPlaystyle.Buildup    => unit.Life > enemy.Life * 1.5f,
                _                      => unit.Life >= enemy.Life * 0.8f,
            };
        }

        private Unit FindNearbyEnemy(Unit unit, Player aiPlayer)
        {
            int radius = GetSearchRadius();
            var tiles  = game.Map.GetTilesInRadius(unit.Position, radius);
            foreach (var tile in tiles)
            {
                if (!IsVisible(tile.Position, aiPlayer)) continue;
                var enemy = tile.Units.FirstOrDefault(u => u.OwnerId != aiPlayer.PlayerId &&
                                                            !(u is Spy sp && !sp.IsRevealed));
                if (enemy != null) return enemy;
            }
            return null;
        }

        private int GetSearchRadius() => playstyle switch
        {
            AIPlaystyle.Aggressive => 8,
            AIPlaystyle.Defensive  => 4,
            AIPlaystyle.Aerial     => 9,
            _ => 6
        };

        private bool IsAdjacent(TilePosition a, TilePosition b)
        {
            int dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
            return dx <= 1 && dy <= 1 && !(dx == 0 && dy == 0);
        }

        private bool IsVisible(TilePosition pos, Player aiPlayer) =>
            aiPlayer.FogOfWar.TryGetValue(pos, out var v) && v == VisibilityLevel.Visible;

        // ─── Combat ──────────────────────────────────────────────────────────────
        private void ExecuteCombat(Unit attacker, Unit defender, Player aiPlayer)
        {
            TilePosition originalPosition = attacker.Position;
            var combatResult = game.CalculateCombat(attacker, defender, originalPosition);

            var defenderOwner = game.Players.FirstOrDefault(p => p.PlayerId == defender.OwnerId);
            var attackerOwner = game.Players.FirstOrDefault(p => p.PlayerId == attacker.OwnerId);

            string attackerOwnerName  = attackerOwner?.Name ?? "Unknown";
            string defenderOwnerName  = defenderOwner?.Name ?? "Unknown";
            string attackerUnitName   = (attacker.IsVeteran ? "veteran " : "") + attacker.GetName().ToLower();
            string defenderUnitName   = (defender.IsVeteran ? "veteran " : "") + defender.GetName().ToLower();

            if (defenderOwner != null && !defenderOwner.IsAI)
                game.PendingCombatReplays.Enqueue(combatResult);

            if (combatResult.AttackerWon)
            {
                var oldTile      = game.Map.GetTile(attacker.Position);
                var defenderTile = game.Map.GetTile(defender.Position);

                oldTile.Units.Remove(attacker);

                bool structureCaptured = false;
                Structure capturedStructure = null;
                if (defenderTile.Structure != null && defenderTile.Structure.OwnerId == defender.OwnerId)
                {
                    capturedStructure  = defenderTile.Structure;
                    structureCaptured  = true;
                    defenderOwner?.RecordStructureLoss(capturedStructure);
                    defenderOwner?.Structures.Remove(capturedStructure);
                    capturedStructure.OwnerId = attacker.OwnerId;
                    capturedStructure.CustomName = EmpireGame.Services.CommanderCityNames.NextCityName(attackerOwner);
                    attackerOwner?.Structures.Add(capturedStructure);
                }

                defenderTile.Units.Remove(defender);
                defenderOwner?.Units.Remove(defender);
                defenderOwner?.RecordUnitLoss(attacker.OwnerId);

                attacker.Position = defender.Position;
                defenderTile.Units.Add(attacker);
                defenderTile.OwnerId = attacker.OwnerId;

                string msg = structureCaptured
                    ? $"⚔️ {defenderOwnerName}'s {defenderUnitName} defeated by {attackerOwnerName}'s {attackerUnitName}, capturing {capturedStructure.GetName()}!"
                    : $"⚔️ {defenderOwnerName}'s {defenderUnitName} defeated by {attackerOwnerName}'s {attackerUnitName}!";
                messageCallback?.Invoke(msg, MessageType.Combat);

                if (defenderOwner != null && game.TurnNumber > 3 &&
                    game.IsPlayerEliminated(defenderOwner) && !defenderOwner.HasBeenEliminated)
                {
                    defenderOwner.HasBeenEliminated = true;
                    messageCallback?.Invoke($"💀 {defenderOwner.Name} has been eliminated!", MessageType.Critical);
                }
            }
            else if (combatResult.DefenderWon)
            {
                var attackerTile = game.Map.GetTile(attacker.Position);
                attackerTile.Units.Remove(attacker);
                attackerOwner?.Units.Remove(attacker);
                attackerOwner?.RecordUnitLoss(defender.OwnerId);
                messageCallback?.Invoke($"⚔️ {attackerOwnerName}'s {attackerUnitName} defeated by {defenderOwnerName}'s {defenderUnitName}!", MessageType.Combat);

                if (attackerOwner != null && game.TurnNumber > 3 &&
                    game.IsPlayerEliminated(attackerOwner) && !attackerOwner.HasBeenEliminated)
                {
                    attackerOwner.HasBeenEliminated = true;
                    messageCallback?.Invoke($"💀 {attackerOwner.Name} has been eliminated!", MessageType.Critical);
                }
            }

            if (attacker.Life > 0) attacker.MovementPoints = 0;
        }

        // ─── Movement ────────────────────────────────────────────────────────────
        private void MoveToward(Unit unit, TilePosition target, Player aiPlayer)
        {
            var path = game.Map.FindPath(unit.Position, target, unit);
            if (path.Count <= 1) return;

            double movementLeft = unit.MovementPoints;
            int step = 1;

            while (step < path.Count && movementLeft >= 0.5)
            {
                var nextTile = game.Map.GetTile(path[step]);
                double cost  = nextTile.GetMovementCost(unit);
                if (cost > movementLeft) break;

                // Attack enemy structure with HP > 0 instead of entering
                if (nextTile.Structure != null &&
                    nextTile.Structure.OwnerId != unit.OwnerId &&
                    nextTile.Structure.Life > 0)
                {
                    var result = game.AttackStructure(unit, nextTile.Structure);
                    string unitOwnerName  = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId)?.Name ?? "Unknown";
                    string sOwnerName     = game.Players.FirstOrDefault(p => p.PlayerId == nextTile.Structure.OwnerId)?.Name ?? "Unknown";

                    if (nextTile.Structure.Life <= 0)
                        messageCallback?.Invoke($"💥 {unitOwnerName}'s {unit.GetName()} destroyed {sOwnerName}'s {nextTile.Structure.GetName()}!", MessageType.Combat);
                    else
                        messageCallback?.Invoke($"💥 {unitOwnerName}'s {unit.GetName()} attacked {sOwnerName}'s {nextTile.Structure.GetName()} ({nextTile.Structure.Life}/{nextTile.Structure.MaxLife} HP)", MessageType.Combat);

                    unit.MovementPoints = 0;
                    break;
                }

                // Stop before engaging enemy units (ExecuteCombat handles it)
                var enemy = nextTile.Units.FirstOrDefault(u => u.OwnerId != unit.OwnerId &&
                                                               !(u is Spy sp && !sp.IsRevealed));
                if (enemy != null) { ExecuteCombat(unit, enemy, aiPlayer); break; }

                bool friendly = nextTile.Units.Any(u => u.OwnerId == unit.OwnerId);
                if (friendly) break;

                var oldTile = game.Map.GetTile(unit.Position);
                oldTile.Units.Remove(unit);
                unit.Position = path[step];
                nextTile.Units.Add(unit);
                nextTile.OwnerId = unit.OwnerId;

                // Capture structure if at 0 HP
                if (nextTile.Structure != null &&
                    nextTile.Structure.OwnerId != unit.OwnerId &&
                    nextTile.Structure.Life <= 0)
                {
                    var cap      = nextTile.Structure;
                    var oldOwner = game.Players.FirstOrDefault(p => p.PlayerId == cap.OwnerId);
                    string unitOwnerName  = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId)?.Name ?? "Unknown";
                    string oldOwnerName   = oldOwner?.Name ?? "Unknown";

                    oldOwner?.RecordStructureLoss(cap);
                    oldOwner?.Structures.Remove(cap);
                    messageCallback?.Invoke($"⚠️ {oldOwnerName} lost {cap.GetName()}!", MessageType.Warning);

                    cap.OwnerId = unit.OwnerId;
                    cap.Life    = cap.MaxLife; // restore HP on capture

                    var newOwner = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId);
                    cap.CustomName = EmpireGame.Services.CommanderCityNames.NextCityName(newOwner);
                    newOwner?.Structures.Add(cap);
                    messageCallback?.Invoke($"🏰 {unitOwnerName}'s {unit.GetName()} captured {cap.GetName()}!", MessageType.Info);
                }

                movementLeft -= cost;
                step++;
            }

            unit.MovementPoints = movementLeft;
        }

        private void ExploreOrDefend(Unit unit, Player aiPlayer)
        {
            var target = FindExplorationTarget(unit, aiPlayer);
            if (target.X != -1) MoveToward(unit, target, aiPlayer);
            else RandomMove(unit);
        }

        private TilePosition FindExplorationTarget(Unit unit, Player aiPlayer)
        {
            int radius = playstyle == AIPlaystyle.Defensive ? 6 : 12;
            var targets = new List<TilePosition>();

            for (int x = unit.Position.X - radius; x <= unit.Position.X + radius; x++)
                for (int y = unit.Position.Y - radius; y <= unit.Position.Y + radius; y++)
                {
                    var pos = new TilePosition(x, y);
                    if (game.Map.IsValidPosition(pos) &&
                        (!aiPlayer.FogOfWar.ContainsKey(pos) || aiPlayer.FogOfWar[pos] == VisibilityLevel.Explored))
                        targets.Add(pos);
                }

            return targets.Count > 0 ? targets[rand.Next(targets.Count)] : new TilePosition(-1, -1);
        }

        private void ReturnToBase(AirUnit airUnit, Player aiPlayer)
        {
            Structure nearest = null;
            int nearestD = int.MaxValue;
            foreach (var s in aiPlayer.Structures)
            {
                if (!(s is Base)) continue;
                int d = Math.Abs(airUnit.Position.X - s.Position.X) + Math.Abs(airUnit.Position.Y - s.Position.Y);
                if (d < nearestD) { nearestD = d; nearest = s; }
            }
            if (nearest != null) MoveToward(airUnit, nearest.Position, aiPlayer);
        }

        private void Patrol(AirUnit unit) => RandomMove(unit);
        private void Patrol(SeaUnit unit) => RandomMove(unit);

        private void RandomMove(Unit unit)
        {
            int[] dx = { -1, 0, 1, 0 };
            int[] dy = {  0, 1, 0, -1 };
            int dir = rand.Next(4);
            var target = new TilePosition(unit.Position.X + dx[dir], unit.Position.Y + dy[dir]);
            if (!game.Map.IsValidPosition(target)) return;
            var tile = game.Map.GetTile(target);
            if (tile.CanUnitEnter(unit) && tile.Units.Count == 0 && tile.MovementCost <= unit.MovementPoints)
            {
                game.Map.GetTile(unit.Position).Units.Remove(unit);
                unit.Position = target;
                unit.MovementPoints -= tile.MovementCost;
                tile.Units.Add(unit);
            }
        }

        // ─── Map helpers ─────────────────────────────────────────────────────────
        private TilePosition? FindAdjacentEmptyLand(TilePosition center)
        {
            foreach (var d in new (int dx, int dy)[] { (-1,-1),(0,-1),(1,-1),(-1,0),(1,0),(-1,1),(0,1),(1,1) })
            {
                var np = new TilePosition(center.X + d.dx, center.Y + d.dy);
                if (!game.Map.IsValidPosition(np)) continue;
                var t = game.Map.GetTile(np);
                if (t.Structure == null && t.Units.Count == 0 &&
                    (t.Terrain == TerrainType.Land || t.Terrain == TerrainType.Plains ||
                     t.Terrain == TerrainType.Forest || t.Terrain == TerrainType.Hills ||
                     t.Terrain == TerrainType.Mountain))
                    return np;
            }
            return null;
        }

        private TilePosition? FindNearestUnminedResource(TilePosition from)
        {
            TilePosition? best = null; int bestD = int.MaxValue;
            for (int x = 0; x < game.Map.Width; x++)
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var t = game.Map.GetTile(new TilePosition(x, y));
                    if (ResourceRegistry.IsMineable(t.Resource) && t.Structure == null)
                    {
                        int d = Math.Abs(x - from.X) + Math.Abs(y - from.Y);
                        if (d < bestD) { bestD = d; best = new TilePosition(x, y); }
                    }
                }
            return best;
        }

        private TilePosition FindGoodBaseLocation(Player aiPlayer, Sapper sapper)
        {
            var candidates = new List<(TilePosition pos, int score)>();

            for (int x = 0; x < game.Map.Width; x++)
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    if (!sapper.CanBuildBaseAt(pos, game.Map)) continue;
                    var tile = game.Map.GetTile(pos);
                    if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater) continue;

                    bool tooClose = game.Players.SelectMany(p => p.Structures.Where(s => s is Base))
                                        .Any(s => Math.Abs(pos.X - s.Position.X) + Math.Abs(pos.Y - s.Position.Y) < 12);
                    if (tooClose) continue;

                    // Score: friendly tiles nearby + resource tiles nearby (for Buildup)
                    int friendlyScore = 0, resourceScore = 0;
                    for (int dx = -5; dx <= 5; dx++)
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            var np = new TilePosition(x + dx, y + dy);
                            if (!game.Map.IsValidPosition(np)) continue;
                            var nt = game.Map.GetTile(np);
                            if (nt.OwnerId == aiPlayer.PlayerId) friendlyScore++;
                            if (ResourceRegistry.IsMineable(nt.Resource) && nt.Structure == null) resourceScore++;
                        }

                    int score = friendlyScore + (playstyle == AIPlaystyle.Buildup ? resourceScore * 3 : resourceScore);
                    if (score > 5) candidates.Add((pos, score));
                }

            if (candidates.Count == 0) return new TilePosition(-1, -1);
            // Best score + closest
            return candidates.OrderByDescending(c => c.score)
                             .ThenBy(c => Math.Abs(c.pos.X - sapper.Position.X) + Math.Abs(c.pos.Y - sapper.Position.Y))
                             .First().pos;
        }

        private TilePosition FindBridgeBuildLocation(Player aiPlayer, Sapper sapper)
        {
            var candidates = new List<TilePosition>();
            for (int dx = -10; dx <= 10; dx++)
                for (int dy = -10; dy <= 10; dy++)
                {
                    var pos = new TilePosition(sapper.Position.X + dx, sapper.Position.Y + dy);
                    if (!game.Map.IsValidPosition(pos) || !sapper.CanBuildBridgeAt(pos, game.Map)) continue;

                    int[] ddx = { -1, 0, 1, 0 }, ddy = { 0, 1, 0, -1 };
                    int friendlyNeighbors = ddx.Zip(ddy, (a, b) =>
                    {
                        var np = new TilePosition(pos.X + a, pos.Y + b);
                        if (!game.Map.IsValidPosition(np)) return 0;
                        var nt = game.Map.GetTile(np);
                        return (nt.OwnerId == aiPlayer.PlayerId || nt.Units.Any(u => u.OwnerId == aiPlayer.PlayerId)) ? 1 : 0;
                    }).Sum();

                    if (friendlyNeighbors >= 2) candidates.Add(pos);
                }

            if (candidates.Count == 0) return new TilePosition(-1, -1);
            return candidates.OrderBy(c => Math.Abs(c.X - sapper.Position.X) + Math.Abs(c.Y - sapper.Position.Y)).First();
        }

        private TilePosition FindCenterOfTerritory(Player aiPlayer)
        {
            var owned = new List<TilePosition>();
            for (int x = 0; x < game.Map.Width; x++)
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    if (game.Map.GetTile(pos).OwnerId == aiPlayer.PlayerId) owned.Add(pos);
                }
            if (owned.Count == 0) return new TilePosition(-1, -1);
            return new TilePosition((int)owned.Average(t => t.X), (int)owned.Average(t => t.Y));
        }
    }
}
