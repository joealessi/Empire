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

        public AIController(Game game, AIPlaystyle playstyle = AIPlaystyle.Balanced)
        {
            this.game = game;
            this.rand = new Random();
            this.playstyle = playstyle;
        }

        public void ExecuteAITurn(Player aiPlayer, Action<string, MessageType> onMessage = null)
        {
            messageCallback = onMessage;

            if (aiPlayer.Personality != null)
            {
                playstyle = aiPlayer.Personality.Playstyle;
            }

            HandleProduction(aiPlayer);
            HandleUnits(aiPlayer);
            HandleSappers(aiPlayer);

            // Check for eliminated players after AI turn
            game.CheckForEliminatedPlayers();
        }

        public void SetPlaystyle(AIPlaystyle newPlaystyle)
        {
            this.playstyle = newPlaystyle;
        }

        public void ExecuteAITurn(Player aiPlayer)
        {
            if (aiPlayer.Personality != null)
            {
                playstyle = aiPlayer.Personality.Playstyle;
            }

            HandleProduction(aiPlayer);
            HandleUnits(aiPlayer);
        }

        private void HandleProduction(Player aiPlayer)
        {
            foreach (var structure in aiPlayer.Structures)
            {
                if (structure is Base baseStructure)
                {
                    if (baseStructure.ProductionQueue.Count < GetProductionQueueSize())
                    {
                        var unitOrder = DecideWhatToBuild(baseStructure, aiPlayer);
                        if (unitOrder != null)
                        {
                            aiPlayer.Gold -= unitOrder.GoldCost;
                            aiPlayer.Steel -= unitOrder.SteelCost;
                            aiPlayer.Oil -= unitOrder.OilCost;

                            baseStructure.ProductionQueue.Enqueue(unitOrder);
                        }
                    }
                }
                else if (structure is City city)
                {
                    if (city.ProductionQueue.Count < GetProductionQueueSize())
                    {
                        var unitOrder = DecideWhatToBuild(city, aiPlayer);
                        if (unitOrder != null)
                        {
                            aiPlayer.Gold -= unitOrder.GoldCost;
                            aiPlayer.Steel -= unitOrder.SteelCost;
                            aiPlayer.Oil -= unitOrder.OilCost;

                            city.ProductionQueue.Enqueue(unitOrder);
                        }
                    }
                }
            }
        }

        private int GetProductionQueueSize()
        {
            return playstyle switch
            {
                AIPlaystyle.Buildup => 3,
                AIPlaystyle.Aggressive => 1,
                _ => 2
            };
        }

        private UnitProductionOrder DecideWhatToBuild(Structure structure, Player aiPlayer)
        {
            bool canProduceNaval = structure is Base baseStructure && baseStructure.CanProduceNaval;

            // Helper to check affordability
            bool CanAfford(int gold, int steel, int oil)
            {
                return aiPlayer.Gold >= gold && aiPlayer.Steel >= steel && aiPlayer.Oil >= oil;
            }

            // Count current units by type
            int armyCount = aiPlayer.Units.Count(u => u is Army);
            int tankCount = aiPlayer.Units.Count(u => u is Tank);
            int artilleryCount = aiPlayer.Units.Count(u => u is Artillery);
            int aaCount = aiPlayer.Units.Count(u => u is AntiAircraft);
            int fighterCount = aiPlayer.Units.Count(u => u is Fighter);
            int bomberCount = aiPlayer.Units.Count(u => u is Bomber);
            int carrierCount = aiPlayer.Units.Count(u => u is Carrier);
            int battleshipCount = aiPlayer.Units.Count(u => u is Battleship);
            int destroyerCount = aiPlayer.Units.Count(u => u is Destroyer);
            int submarineCount = aiPlayer.Units.Count(u => u is Submarine);
            int patrolBoatCount = aiPlayer.Units.Count(u => u is PatrolBoat);
            int transportCount = aiPlayer.Units.Count(u => u is Transport);
            int tankerCount = aiPlayer.Units.Count(u => u is Tanker);
            int spyCount = aiPlayer.Units.Count(u => u is Spy);
            int sapperCount = aiPlayer.Units.Count(u => u is Sapper);

            int totalLandUnits = armyCount + tankCount + artilleryCount + aaCount;
            int totalAirUnits = fighterCount + bomberCount + tankerCount;
            int totalNavalUnits = carrierCount + battleshipCount + destroyerCount + submarineCount + patrolBoatCount + transportCount;

            // Personality-based building priorities
            switch (playstyle)
            {
                case AIPlaystyle.Aggressive:
                    // Focus on offensive units
                    if (tankCount < 5 && CanAfford(2, 1, 0))
                        return new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank");
                    if (bomberCount < 3 && CanAfford(5, 1, 2))
                        return new UnitProductionOrder(typeof(Bomber), 5, 1, 2, "Bomber");
                    if (armyCount < 8 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                    break;

                case AIPlaystyle.Defensive:
                    // Focus on defensive units
                    if (aaCount < 3 && CanAfford(2, 1, 0))
                        return new UnitProductionOrder(typeof(AntiAircraft), 2, 1, 0, "AntiAircraft");
                    if (artilleryCount < 3 && CanAfford(2, 1, 0))
                        return new UnitProductionOrder(typeof(Artillery), 2, 1, 0, "Artillery");
                    if (fighterCount < 4 && CanAfford(3, 0, 1))
                        return new UnitProductionOrder(typeof(Fighter), 3, 0, 1, "Fighter");
                    if (armyCount < 6 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                    break;

                case AIPlaystyle.Naval:
                    // Focus on naval units
                    if (canProduceNaval)
                    {
                        if (carrierCount < 2 && CanAfford(8, 2, 2))
                            return new UnitProductionOrder(typeof(Carrier), 8, 2, 2, "Carrier");
                        if (battleshipCount < 3 && CanAfford(6, 2, 1))
                            return new UnitProductionOrder(typeof(Battleship), 6, 2, 1, "Battleship");
                        if (destroyerCount < 4 && CanAfford(4, 1, 1))
                            return new UnitProductionOrder(typeof(Destroyer), 4, 1, 1, "Destroyer");
                        if (submarineCount < 3 && CanAfford(4, 1, 1))
                            return new UnitProductionOrder(typeof(Submarine), 4, 1, 1, "Submarine");
                        if (transportCount < 2 && CanAfford(3, 1, 1))
                            return new UnitProductionOrder(typeof(Transport), 3, 1, 1, "Transport");
                    }
                    // Still need some land units
                    if (armyCount < 4 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                    break;

                case AIPlaystyle.Aerial:
                    // Focus on air units
                    if (fighterCount < 5 && CanAfford(3, 0, 1))
                        return new UnitProductionOrder(typeof(Fighter), 3, 0, 1, "Fighter");
                    if (bomberCount < 4 && CanAfford(5, 1, 2))
                        return new UnitProductionOrder(typeof(Bomber), 5, 1, 2, "Bomber");
                    if (tankerCount < 2 && CanAfford(3, 0, 1))
                        return new UnitProductionOrder(typeof(Tanker), 3, 0, 1, "Tanker");
                    if (carrierCount < 2 && canProduceNaval && CanAfford(8, 2, 2))
                        return new UnitProductionOrder(typeof(Carrier), 8, 2, 2, "Carrier");
                    // Need some land units for bases
                    if (armyCount < 3 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                    break;

                case AIPlaystyle.Buildup:
                    // Balanced buildup with emphasis on economy
                    if (spyCount == 0 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Spy), 2, 0, 0, "Spy");
                    if (armyCount < 4 && CanAfford(2, 0, 0))
                        return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                    if (tankCount < 2 && CanAfford(2, 1, 0))
                        return new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank");
                    if (fighterCount < 2 && CanAfford(3, 0, 1))
                        return new UnitProductionOrder(typeof(Fighter), 3, 0, 1, "Fighter");
                    break;
            }

            // Build sappers for expansion if we have fewer than 2 bases
            int baseCount = aiPlayer.Structures.Count(s => s is Base);
            if (baseCount < 2)
            {
                if (sapperCount == 0 && CanAfford(2, 1, 0))
                {
                    return new UnitProductionOrder(typeof(Sapper), 2, 1, 0, "Sapper");
                }
            }

            // Ensure we have at least one spy for intelligence
            if (spyCount == 0 && CanAfford(2, 0, 0))
                return new UnitProductionOrder(typeof(Spy), 2, 0, 0, "Spy");

            // Balanced army composition as fallback
            if (totalLandUnits < 10)
            {
                if (armyCount < 6 && CanAfford(2, 0, 0))
                    return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                if (tankCount < 3 && CanAfford(2, 1, 0))
                    return new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank");
                if (artilleryCount < 2 && CanAfford(2, 1, 0))
                    return new UnitProductionOrder(typeof(Artillery), 2, 1, 0, "Artillery");
            }

            // Air support
            if (totalAirUnits < 5)
            {
                if (fighterCount < 3 && CanAfford(3, 0, 1))
                    return new UnitProductionOrder(typeof(Fighter), 3, 0, 1, "Fighter");
                if (bomberCount < 2 && CanAfford(5, 1, 2))
                    return new UnitProductionOrder(typeof(Bomber), 5, 1, 2, "Bomber");
            }

            // Naval units if we can produce them
            if (canProduceNaval && totalNavalUnits < 5)
            {
                if (patrolBoatCount < 2 && CanAfford(2, 1, 0))
                    return new UnitProductionOrder(typeof(PatrolBoat), 2, 1, 0, "PatrolBoat");
                if (destroyerCount < 2 && CanAfford(4, 1, 1))
                    return new UnitProductionOrder(typeof(Destroyer), 4, 1, 1, "Destroyer");
            }

            // Default: build army if we can afford it
            if (CanAfford(2, 0, 0))
                return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");

            return null;
        }

        private void HandleUnits(Player aiPlayer)
        {
            var units = aiPlayer.Units.ToList();

            foreach (var unit in units)
            {
                if (unit.MovementPoints <= 0)
                    continue;

                // Priority 1: Check for lost structures to recapture
                var lostStructureTarget = FindLostStructureToRecapture(unit, aiPlayer);
                if (lostStructureTarget != null && ShouldRecaptureStructure(aiPlayer, lostStructureTarget))
                {
                    MoveToward(unit, lostStructureTarget.Position, aiPlayer);
                    continue;
                }

                // Priority 2: Check for enemy structures to capture
                var enemyStructure = FindNearbyEnemyStructure(unit, aiPlayer);
                if (enemyStructure != null && ShouldCaptureStructure(unit, enemyStructure, aiPlayer))
                {
                    MoveToward(unit, enemyStructure.Position, aiPlayer);
                    continue;
                }

                // Priority 3: Check for enemy units
                var enemyTarget = FindNearbyEnemy(unit, aiPlayer);
                if (enemyTarget != null)
                {
                    bool shouldEngage = ShouldEngageEnemy(unit, enemyTarget, aiPlayer);

                    if (shouldEngage && IsAdjacent(unit.Position, enemyTarget.Position))
                    {
                        ExecuteCombat(unit, enemyTarget, aiPlayer);
                    }
                    else if (shouldEngage)
                    {
                        MoveToward(unit, enemyTarget.Position, aiPlayer);
                    }
                    else
                    {
                        ExploreOrDefend(unit, aiPlayer);
                    }
                }
                else
                {
                    if (unit is Army || unit is Tank)
                    {
                        ExploreOrDefend(unit, aiPlayer);
                    }
                    else if (unit is AirUnit airUnit)
                    {
                        if (airUnit.Fuel < airUnit.MaxFuel / 2)
                        {
                            ReturnToBase(airUnit, aiPlayer);
                        }
                        else
                        {
                            Patrol(airUnit);
                        }
                    }
                }
            }
        }

        private Structure FindLostStructureToRecapture(Unit unit, Player aiPlayer)
        {
            if (aiPlayer.LostStructures.Count == 0)
                return null;

            Structure nearest = null;
            int nearestDistance = int.MaxValue;

            foreach (var lostStructure in aiPlayer.LostStructures)
            {
                if (!game.Map.IsValidPosition(lostStructure.Position))
                    continue;

                int distance = Math.Abs(unit.Position.X - lostStructure.Position.X) +
                             Math.Abs(unit.Position.Y - lostStructure.Position.Y);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = lostStructure;
                }
            }

            return nearest;
        }

        private bool ShouldRecaptureStructure(Player aiPlayer, Structure lostStructure)
        {
            int remainingStructures = aiPlayer.Structures.Count;

            switch (playstyle)
            {
                case AIPlaystyle.Aggressive:
                    return true; // Always try to recapture

                case AIPlaystyle.Balanced:
                    return true; // Try to recapture

                case AIPlaystyle.Defensive:
                    if (remainingStructures == 0)
                        return true; // Do anything to get it back
                    return false; // Don't try if we have other structures

                default:
                    return remainingStructures == 0;
            }
        }

        private Structure FindNearbyEnemyStructure(Unit unit, Player aiPlayer)
        {
            int searchRadius = GetSearchRadius();
            var tiles = game.Map.GetTilesInRadius(unit.Position, searchRadius);

            foreach (var tile in tiles)
            {
                if (!aiPlayer.FogOfWar.ContainsKey(tile.Position) ||
                    aiPlayer.FogOfWar[tile.Position] != VisibilityLevel.Visible)
                {
                    continue;
                }

                if (tile.Structure != null && tile.Structure.OwnerId != aiPlayer.PlayerId)
                {
                    return tile.Structure;
                }
            }

            return null;
        }

        private bool ShouldCaptureStructure(Unit unit, Structure enemyStructure, Player aiPlayer)
        {
            var tile = game.Map.GetTile(enemyStructure.Position);
            var defenders = tile.Units.Where(u => u.OwnerId == enemyStructure.OwnerId).ToList();

            double offensivePower = unit.Life * unit.Power;
            double defensivePower = defenders.Sum(d => d.Life * d.Toughness);

            int aggressionBonus = aiPlayer.GetAggressionTowardsPlayer(enemyStructure.OwnerId);

            switch (playstyle)
            {
                case AIPlaystyle.Aggressive:
                    return true; // Always go for it

                case AIPlaystyle.Balanced:
                    if (aggressionBonus > 0)
                        return offensivePower >= defensivePower * 0.7; // More aggressive if provoked
                    return offensivePower >= defensivePower * 0.9; // Need decent odds

                case AIPlaystyle.Defensive:
                    return offensivePower >= defensivePower * 1.5; // Need overwhelming odds

                default:
                    return offensivePower >= defensivePower;
            }
        }

        private bool ShouldEngageEnemy(Unit unit, Unit enemy, Player aiPlayer)
        {
            int aggressionBonus = aiPlayer.GetAggressionTowardsPlayer(enemy.OwnerId);

            switch (playstyle)
            {
                case AIPlaystyle.Aggressive:
                    return true;

                case AIPlaystyle.Balanced:
                    if (aggressionBonus > 0)
                        return unit.Life >= enemy.Life * 0.7; // More willing if provoked
                    return unit.Life >= enemy.Life * 0.8;

                case AIPlaystyle.Defensive:
                    return unit.Life > enemy.Life * 1.2;

                case AIPlaystyle.Buildup:
                    return unit.Life > enemy.Life * 1.5;

                default:
                    return unit.Life >= enemy.Life * 0.8;
            }
        }

        private Unit FindNearbyEnemy(Unit unit, Player aiPlayer)
        {
            int searchRadius = GetSearchRadius();
            var tiles = game.Map.GetTilesInRadius(unit.Position, searchRadius);

            foreach (var tile in tiles)
            {
                if (!aiPlayer.FogOfWar.ContainsKey(tile.Position) ||
                    aiPlayer.FogOfWar[tile.Position] != VisibilityLevel.Visible)
                {
                    continue;
                }

                foreach (var enemyUnit in tile.Units)
                {
                    if (enemyUnit.OwnerId != aiPlayer.PlayerId)
                    {
                        return enemyUnit;
                    }
                }
            }

            return null;
        }

        private int GetSearchRadius()
        {
            return playstyle switch
            {
                AIPlaystyle.Aggressive => 7,
                AIPlaystyle.Defensive => 3,
                _ => 5
            };
        }

        private bool IsAdjacent(TilePosition pos1, TilePosition pos2)
        {
            int dx = Math.Abs(pos1.X - pos2.X);
            int dy = Math.Abs(pos1.Y - pos2.Y);
            return (dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0);
        }

        private void ExecuteCombat(Unit attacker, Unit defender, Player aiPlayer)
        {
            TilePosition originalPosition = attacker.Position;
            var combatResult = game.CalculateCombat(attacker, defender, originalPosition);

            var defenderOwner = game.Players.FirstOrDefault(p => p.PlayerId == defender.OwnerId);
            var attackerOwner = game.Players.FirstOrDefault(p => p.PlayerId == attacker.OwnerId);

            string attackerOwnerName = attackerOwner?.Name ?? "Unknown";
            string defenderOwnerName = defenderOwner?.Name ?? "Unknown";

            string attackerUnitName = attacker.GetName().ToLower();
            string defenderUnitName = defender.GetName().ToLower();

            if (attacker.IsVeteran)
                attackerUnitName = "veteran " + attackerUnitName;
            if (defender.IsVeteran)
                defenderUnitName = "veteran " + defenderUnitName;

            if (combatResult.AttackerWon)
            {
                var oldTile = game.Map.GetTile(attacker.Position);
                oldTile.Units.Remove(attacker);

                var defenderTile = game.Map.GetTile(defender.Position);

                // Check if defender's structure is being captured
                bool structureCaptured = false;
                Structure capturedStructure = null;
                if (defenderTile.Structure != null && defenderTile.Structure.OwnerId == defender.OwnerId)
                {
                    capturedStructure = defenderTile.Structure;
                    structureCaptured = true;

                    if (defenderOwner != null)
                    {
                        defenderOwner.RecordStructureLoss(capturedStructure);
                        defenderOwner.Structures.Remove(capturedStructure);
                    }

                    capturedStructure.OwnerId = attacker.OwnerId;
                    if (attackerOwner != null)
                    {
                        attackerOwner.Structures.Add(capturedStructure);
                    }
                }

                defenderTile.Units.Remove(defender);

                if (defenderOwner != null)
                {
                    defenderOwner.Units.Remove(defender);
                    defenderOwner.RecordUnitLoss(attacker.OwnerId);
                }

                attacker.Position = defender.Position;
                defenderTile.Units.Add(attacker);
                defenderTile.OwnerId = attacker.OwnerId;

                // Report combat result
                if (structureCaptured)
                {
                    messageCallback?.Invoke($"⚔️ {defenderOwnerName}'s {defenderUnitName} was defeated by {attackerOwnerName}'s {attackerUnitName}, capturing {capturedStructure.GetName()}!", MessageType.Combat);
                }
                else
                {
                    messageCallback?.Invoke($"⚔️ {defenderOwnerName}'s {defenderUnitName} was defeated by {attackerOwnerName}'s {attackerUnitName}!", MessageType.Combat);
                }

                // Check if defender player was eliminated
                if (defenderOwner != null && game.TurnNumber > 3 && game.IsPlayerEliminated(defenderOwner) && !defenderOwner.HasBeenEliminated)
                {
                    defenderOwner.HasBeenEliminated = true;
                    messageCallback?.Invoke($"💀 {defenderOwner.Name} has been eliminated from the game!", MessageType.Critical);
                }
            }
            else if (combatResult.DefenderWon)
            {
                var attackerTile = game.Map.GetTile(attacker.Position);
                attackerTile.Units.Remove(attacker);

                if (attackerOwner != null)
                {
                    attackerOwner.Units.Remove(attacker);
                    attackerOwner.RecordUnitLoss(defender.OwnerId);
                }

                // Report combat result
                messageCallback?.Invoke($"⚔️ {attackerOwnerName}'s {attackerUnitName} was defeated by {defenderOwnerName}'s {defenderUnitName}!", MessageType.Combat);

                // Check if attacker player was eliminated
                if (attackerOwner != null && game.TurnNumber > 3 && game.IsPlayerEliminated(attackerOwner) && !attackerOwner.HasBeenEliminated)
                {
                    attackerOwner.HasBeenEliminated = true;
                    messageCallback?.Invoke($"💀 {attackerOwner.Name} has been eliminated from the game!", MessageType.Critical);
                }
            }

            if (attacker.Life > 0)
            {
                attacker.MovementPoints = 0;
            }
        }

        private void MoveToward(Unit unit, TilePosition target, Player aiPlayer)
        {
            var path = game.Map.FindPath(unit.Position, target, unit);

            if (path.Count > 1)
            {
                double movementLeft = unit.MovementPoints;
                int currentStep = 1;

                while (currentStep < path.Count && movementLeft >= 0.5)
                {
                    var nextTile = game.Map.GetTile(path[currentStep]);
                    double cost = nextTile.GetMovementCost(unit);

                    if (cost <= movementLeft)
                    {
                        // CHECK FOR ENEMY STRUCTURE TO ATTACK - ADD THIS SECTION
                        if (nextTile.Structure != null &&
                            nextTile.Structure.OwnerId != unit.OwnerId &&
                            nextTile.Structure.Life > 0)
                        {
                            // Attack the structure
                            var result = game.AttackStructure(unit, nextTile.Structure);

                            string unitOwnerName = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId)?.Name ?? "Unknown";
                            string structureOwnerName = game.Players.FirstOrDefault(p => p.PlayerId == nextTile.Structure.OwnerId)?.Name ?? "Unknown";

                            if (nextTile.Structure.Life <= 0)
                            {
                                messageCallback?.Invoke($"💥 {unitOwnerName}'s {unit.GetName()} destroyed {structureOwnerName}'s {nextTile.Structure.GetName()}!", MessageType.Combat);
                            }
                            else
                            {
                                messageCallback?.Invoke($"💥 {unitOwnerName}'s {unit.GetName()} attacked {structureOwnerName}'s {nextTile.Structure.GetName()} ({nextTile.Structure.Life}/{nextTile.Structure.MaxLife} HP)", MessageType.Combat);
                            }

                            unit.MovementPoints = 0;
                            break;
                        }
                        // END OF STRUCTURE ATTACK SECTION

                        // Existing enemy unit check
                        var enemyUnit = nextTile.Units.FirstOrDefault(u => u.OwnerId != unit.OwnerId);
                        if (enemyUnit != null)
                        {
                            ExecuteCombat(unit, enemyUnit, aiPlayer);
                            break;
                        }

                        bool occupiedByFriendly = nextTile.Units.Any(u => u.OwnerId == unit.OwnerId);
                        if (occupiedByFriendly)
                            break;

                        var oldTile = game.Map.GetTile(unit.Position);
                        oldTile.Units.Remove(unit);

                        unit.Position = path[currentStep];
                        nextTile.Units.Add(unit);
                        nextTile.OwnerId = unit.OwnerId;

                        // Check if we captured a structure (only if structure life is 0)
                        if (nextTile.Structure != null &&
                            nextTile.Structure.OwnerId != unit.OwnerId &&
                            nextTile.Structure.Life <= 0)  // ADD THIS CONDITION
                        {
                            var capturedStructure = nextTile.Structure;
                            var oldOwner = game.Players.FirstOrDefault(p => p.PlayerId == capturedStructure.OwnerId);

                            string unitOwnerName = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId)?.Name ?? "Unknown";
                            string oldOwnerName = oldOwner?.Name ?? "Unknown";

                            if (oldOwner != null)
                            {
                                oldOwner.RecordStructureLoss(capturedStructure);
                                oldOwner.Structures.Remove(capturedStructure);
                                messageCallback?.Invoke($"⚠️ {oldOwnerName} lost {capturedStructure.GetName()}!", MessageType.Warning);
                            }

                            capturedStructure.OwnerId = unit.OwnerId;
                            capturedStructure.Life = capturedStructure.MaxLife;  // ADD THIS - Restore structure to full health when captured

                            var newOwner = game.Players.FirstOrDefault(p => p.PlayerId == unit.OwnerId);
                            if (newOwner != null)
                            {
                                newOwner.Structures.Add(capturedStructure);
                            }

                            messageCallback?.Invoke($"🏰 {unitOwnerName}'s {unit.GetName()} captured {capturedStructure.GetName()}!", MessageType.Info);
                        }

                        movementLeft -= cost;
                        currentStep++;
                    }
                    else
                    {
                        break;
                    }
                }

                unit.MovementPoints = movementLeft;
            }
        }
        private void ExploreOrDefend(Unit unit, Player aiPlayer)
        {
            TilePosition targetPos = FindExplorationTarget(unit, aiPlayer);

            if (targetPos.X != -1)
            {
                MoveToward(unit, targetPos, aiPlayer);
            }
            else
            {
                RandomMove(unit);
            }
        }

        private TilePosition FindExplorationTarget(Unit unit, Player aiPlayer)
        {
            int searchRadius = playstyle == AIPlaystyle.Defensive ? 5 : 10;

            List<TilePosition> targets = new List<TilePosition>();

            for (int x = unit.Position.X - searchRadius; x <= unit.Position.X + searchRadius; x++)
            {
                for (int y = unit.Position.Y - searchRadius; y <= unit.Position.Y + searchRadius; y++)
                {
                    var pos = new TilePosition(x, y);
                    if (game.Map.IsValidPosition(pos))
                    {
                        if (!aiPlayer.FogOfWar.ContainsKey(pos) ||
                            aiPlayer.FogOfWar[pos] == VisibilityLevel.Explored)
                        {
                            targets.Add(pos);
                        }
                    }
                }
            }

            if (targets.Count > 0)
            {
                return targets[rand.Next(targets.Count)];
            }

            return new TilePosition(-1, -1);
        }

        private void RandomMove(Unit unit)
        {
            int[] dx = { -1, 0, 1, 0 };
            int[] dy = { 0, 1, 0, -1 };

            int dir = rand.Next(4);
            var targetPos = new TilePosition(unit.Position.X + dx[dir], unit.Position.Y + dy[dir]);

            if (game.Map.IsValidPosition(targetPos))
            {
                var targetTile = game.Map.GetTile(targetPos);
                if (targetTile.CanUnitEnter(unit) &&
                    targetTile.Units.Count == 0 &&
                    targetTile.MovementCost <= unit.MovementPoints)
                {
                    var oldTile = game.Map.GetTile(unit.Position);
                    oldTile.Units.Remove(unit);

                    unit.Position = targetPos;
                    unit.MovementPoints -= targetTile.MovementCost;
                    targetTile.Units.Add(unit);
                }
            }
        }

        private void ReturnToBase(AirUnit airUnit, Player aiPlayer)
        {
            Structure nearestBase = null;
            int nearestDistance = int.MaxValue;

            foreach (var structure in aiPlayer.Structures)
            {
                if (structure is Base)
                {
                    int distance = Math.Abs(airUnit.Position.X - structure.Position.X) +
                                 Math.Abs(airUnit.Position.Y - structure.Position.Y);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestBase = structure;
                    }
                }
            }

            if (nearestBase != null)
            {
                MoveToward(airUnit, nearestBase.Position, aiPlayer);
            }
        }

        private void Patrol(AirUnit airUnit)
        {
            RandomMove(airUnit);
        }

        private void HandleSappers(Player aiPlayer)
        {
            var sappers = aiPlayer.Units.OfType<Sapper>().ToList();

            foreach (var sapper in sappers)
            {
                // Skip if already building
                if (sapper.IsBuildingBase || sapper.IsBuildingBridge)
                    continue;

                if (sapper.MovementPoints <= 0)
                    continue;

                // Priority 1: Build a base if AI has few bases and sapper is in a good location
                int baseCount = aiPlayer.Structures.Count(s => s is Base);
                if (baseCount < 3 && sapper.CanBuildBaseAt(sapper.Position, game.Map))
                {
                    // Check if this location is relatively safe (no enemies very close)
                    bool isSafe = true;
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        for (int dy = -3; dy <= 3; dy++)
                        {
                            var checkPos = new TilePosition(sapper.Position.X + dx, sapper.Position.Y + dy);
                            if (game.Map.IsValidPosition(checkPos))
                            {
                                var tile = game.Map.GetTile(checkPos);
                                if (tile.Units.Any(u => u.OwnerId != aiPlayer.PlayerId))
                                {
                                    isSafe = false;
                                    break;
                                }
                            }
                        }
                        if (!isSafe) break;
                    }

                    if (isSafe)
                    {
                        sapper.StartBuildingBase(sapper.Position);
                        messageCallback?.Invoke($"🏗️ {aiPlayer.Name}'s Sapper began building a base.", MessageType.Info);
                        continue;
                    }
                }

                // Priority 2: Move toward a good base building location
                if (baseCount < 3)
                {
                    var targetLocation = FindGoodBaseLocation(aiPlayer, sapper);
                    if (targetLocation.X != -1)
                    {
                        MoveToward(sapper, targetLocation, aiPlayer);
                        continue;
                    }
                }

                // Priority 3: Look for bridge building opportunities near friendly territory
                var bridgeLocation = FindBridgeBuildLocation(aiPlayer, sapper);
                if (bridgeLocation.X != -1)
                {
                    if (sapper.CanBuildBridgeAt(bridgeLocation, game.Map))
                    {
                        sapper.StartBuildingBridge(bridgeLocation);
                        messageCallback?.Invoke($"🌉 {aiPlayer.Name}'s Sapper began building a bridge.", MessageType.Info);
                        continue;
                    }
                    else
                    {
                        // Move toward the bridge location
                        MoveToward(sapper, bridgeLocation, aiPlayer);
                        continue;
                    }
                }

                // Default: Move toward center of AI territory for expansion
                var centerOfTerritory = FindCenterOfTerritory(aiPlayer);
                if (centerOfTerritory.X != -1)
                {
                    MoveToward(sapper, centerOfTerritory, aiPlayer);
                }
            }
        }

        private TilePosition FindGoodBaseLocation(Player aiPlayer, Sapper sapper)
        {
            // Look for land tiles near AI territory but not too close to existing bases
            var candidates = new List<TilePosition>();

            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);

                    if (!sapper.CanBuildBaseAt(pos, game.Map))
                        continue;

                    var tile = game.Map.GetTile(pos);

                    // Must be on land
                    if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater)
                        continue;

                    // Must not be too close to existing bases
                    bool tooClose = false;
                    foreach (var structure in game.Players.SelectMany(p => p.Structures.Where(s => s is Base)))
                    {
                        int distance = Math.Abs(pos.X - structure.Position.X) + Math.Abs(pos.Y - structure.Position.Y);
                        if (distance < 15)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                        continue;

                    // Prefer locations near friendly territory
                    int friendlyTilesNearby = 0;
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            var nearPos = new TilePosition(x + dx, y + dy);
                            if (game.Map.IsValidPosition(nearPos))
                            {
                                var nearTile = game.Map.GetTile(nearPos);
                                if (nearTile.OwnerId == aiPlayer.PlayerId)
                                {
                                    friendlyTilesNearby++;
                                }
                            }
                        }
                    }

                    if (friendlyTilesNearby > 10)
                    {
                        candidates.Add(pos);
                    }
                }
            }

            if (candidates.Count == 0)
                return new TilePosition(-1, -1);

            // Return closest candidate to sapper
            return candidates.OrderBy(c => Math.Abs(c.X - sapper.Position.X) + Math.Abs(c.Y - sapper.Position.Y)).First();
        }

        private TilePosition FindBridgeBuildLocation(Player aiPlayer, Sapper sapper)
        {
            // Look for single-tile water near AI territory that would be useful to bridge
            var candidates = new List<TilePosition>();

            // Search in a radius around sapper
            for (int dx = -10; dx <= 10; dx++)
            {
                for (int dy = -10; dy <= 10; dy++)
                {
                    var pos = new TilePosition(sapper.Position.X + dx, sapper.Position.Y + dy);

                    if (!game.Map.IsValidPosition(pos))
                        continue;

                    if (!sapper.CanBuildBridgeAt(pos, game.Map))
                        continue;

                    // Check if this bridge would connect friendly territories
                    var tile = game.Map.GetTile(pos);
                    int friendlyNeighbors = 0;

                    int[] dxCheck = { -1, 0, 1, 0 };
                    int[] dyCheck = { 0, 1, 0, -1 };

                    for (int i = 0; i < 4; i++)
                    {
                        var neighborPos = new TilePosition(pos.X + dxCheck[i], pos.Y + dyCheck[i]);
                        if (game.Map.IsValidPosition(neighborPos))
                        {
                            var neighbor = game.Map.GetTile(neighborPos);
                            if (neighbor.OwnerId == aiPlayer.PlayerId ||
                                neighbor.Units.Any(u => u.OwnerId == aiPlayer.PlayerId))
                            {
                                friendlyNeighbors++;
                            }
                        }
                    }

                    // Bridge is useful if it connects friendly areas
                    if (friendlyNeighbors >= 2)
                    {
                        candidates.Add(pos);
                    }
                }
            }

            if (candidates.Count == 0)
                return new TilePosition(-1, -1);

            // Return closest candidate
            return candidates.OrderBy(c => Math.Abs(c.X - sapper.Position.X) + Math.Abs(c.Y - sapper.Position.Y)).First();
        }

        private TilePosition FindCenterOfTerritory(Player aiPlayer)
        {
            var ownedTiles = new List<TilePosition>();

            for (int x = 0; x < game.Map.Width; x++)
            {
                for (int y = 0; y < game.Map.Height; y++)
                {
                    var pos = new TilePosition(x, y);
                    var tile = game.Map.GetTile(pos);

                    if (tile.OwnerId == aiPlayer.PlayerId)
                    {
                        ownedTiles.Add(pos);
                    }
                }
            }

            if (ownedTiles.Count == 0)
                return new TilePosition(-1, -1);

            int avgX = (int)ownedTiles.Average(t => t.X);
            int avgY = (int)ownedTiles.Average(t => t.Y);

            return new TilePosition(avgX, avgY);
        }
    }
}