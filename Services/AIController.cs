using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpireGame
{
    public class AIController
    {
        private Game game;
        private Random rand;

        public AIController(Game game)
        {
            this.game = game;
            this.rand = new Random();
        }

        public void ExecuteAITurn(Player aiPlayer)
        {
            // Phase 1: Production - queue up units at bases
            HandleProduction(aiPlayer);

            // Phase 2: Movement and combat
            HandleUnits(aiPlayer);
        }

        private void HandleProduction(Player aiPlayer)
        {
            foreach (var structure in aiPlayer.Structures)
            {
                if (structure is Base baseStructure)
                {
                    // If production queue is empty or small, add something
                    if (baseStructure.ProductionQueue.Count < 2)
                    {
                        var unitOrder = DecideWhatToBuild(baseStructure, aiPlayer);
                        if (unitOrder != null)
                        {
                            // Deduct resources immediately when adding to queue
                            aiPlayer.Gold -= unitOrder.GoldCost;
                            aiPlayer.Steel -= unitOrder.SteelCost;
                            aiPlayer.Oil -= unitOrder.OilCost;

                            baseStructure.ProductionQueue.Enqueue(unitOrder);
                        }
                    }
                }
                else if (structure is City city)
                {
                    if (city.ProductionQueue.Count < 2)
                    {
                        var unitOrder = DecideWhatToBuild(city, aiPlayer);
                        if (unitOrder != null)
                        {
                            // Deduct resources immediately when adding to queue
                            aiPlayer.Gold -= unitOrder.GoldCost;
                            aiPlayer.Steel -= unitOrder.SteelCost;
                            aiPlayer.Oil -= unitOrder.OilCost;

                            city.ProductionQueue.Enqueue(unitOrder);
                        }
                    }
                }
            }
        }

        private UnitProductionOrder DecideWhatToBuild(Structure structure, Player aiPlayer)
        {
            // Check what we can actually build based on capacity
            var baseStructure = structure as Base;
            var city = structure as City;

            // Simple AI logic - build a balanced army
            int armies = aiPlayer.Units.Count(u => u is Army);
            int tanks = aiPlayer.Units.Count(u => u is Tank);
            int fighters = aiPlayer.Units.Count(u => u is Fighter);

            // Helper function to check if we can afford a unit
            bool CanAfford(int gold, int steel, int oil)
            {
                return aiPlayer.Gold >= gold && aiPlayer.Steel >= steel && aiPlayer.Oil >= oil;
            }

            // Try to build priority units if capacity allows AND we can afford them
            if (armies < 5)
            {
                if (baseStructure != null && baseStructure.CanBuildUnit(typeof(Army)) && CanAfford(2, 0, 0))
                    return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
                else if (city != null && city.CanBuildUnit(typeof(Army)) && CanAfford(2, 0, 0))
                    return new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army");
            }

            if (tanks < 3)
            {
                if (baseStructure != null && baseStructure.CanBuildUnit(typeof(Tank)) && CanAfford(2, 1, 0))
                    return new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank");
                else if (city != null && city.CanBuildUnit(typeof(Tank)) && CanAfford(2, 1, 0))
                    return new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank");
            }

            if (fighters < 2)
            {
                if (baseStructure != null && baseStructure.CanBuildUnit(typeof(Fighter)) && CanAfford(3, 1, 1))
                    return new UnitProductionOrder(typeof(Fighter), 3, 1, 1, "Fighter");
                else if (city != null && city.CanBuildUnit(typeof(Fighter)) && CanAfford(3, 1, 1))
                    return new UnitProductionOrder(typeof(Fighter), 3, 1, 1, "Fighter");
            }

            if (baseStructure != null && baseStructure.HasShipyard && rand.NextDouble() < 0.3)
            {
                if (baseStructure.CanBuildUnit(typeof(Destroyer)) && CanAfford(3, 2, 1))
                    return new UnitProductionOrder(typeof(Destroyer), 3, 2, 1, "Destroyer");
            }

            // Random choice among what we can build AND afford
            var options = new List<UnitProductionOrder>();

            if (baseStructure != null)
            {
                if (baseStructure.CanBuildUnit(typeof(Army)) && CanAfford(2, 0, 0))
                    options.Add(new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army"));
                if (baseStructure.CanBuildUnit(typeof(Tank)) && CanAfford(2, 1, 0))
                    options.Add(new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank"));
                if (baseStructure.CanBuildUnit(typeof(Artillery)) && CanAfford(2, 1, 0))
                    options.Add(new UnitProductionOrder(typeof(Artillery), 2, 1, 0, "Artillery"));
                if (baseStructure.CanBuildUnit(typeof(Fighter)) && CanAfford(3, 1, 1))
                    options.Add(new UnitProductionOrder(typeof(Fighter), 3, 1, 1, "Fighter"));
            }
            else if (city != null)
            {
                if (city.CanBuildUnit(typeof(Army)) && CanAfford(2, 0, 0))
                    options.Add(new UnitProductionOrder(typeof(Army), 2, 0, 0, "Army"));
                if (city.CanBuildUnit(typeof(Tank)) && CanAfford(2, 1, 0))
                    options.Add(new UnitProductionOrder(typeof(Tank), 2, 1, 0, "Tank"));
                if (city.CanBuildUnit(typeof(Artillery)) && CanAfford(2, 1, 0))
                    options.Add(new UnitProductionOrder(typeof(Artillery), 2, 1, 0, "Artillery"));
                if (city.CanBuildUnit(typeof(Fighter)) && CanAfford(3, 1, 1))
                    options.Add(new UnitProductionOrder(typeof(Fighter), 3, 1, 1, "Fighter"));
            }

            if (options.Count > 0)
                return options[rand.Next(options.Count)];

            return null;
        }

        private void HandleUnits(Player aiPlayer)
        {
            // Make a copy of the units list since we might modify it
            var units = aiPlayer.Units.ToList();

            foreach (var unit in units)
            {
                if (unit.MovementPoints <= 0)
                    continue;

                // Check for nearby enemies to attack
                var enemyTarget = FindNearbyEnemy(unit, aiPlayer);
                if (enemyTarget != null)
                {
                    // Try to attack or move toward enemy
                    if (IsAdjacent(unit.Position, enemyTarget.Position))
                    {
                        // Attack!
                        ExecuteCombat(unit, enemyTarget);
                    }
                    else
                    {
                        // Move toward enemy
                        MoveToward(unit, enemyTarget.Position);
                    }
                }
                else
                {
                    // No enemies nearby - explore or defend
                    if (unit is Army || unit is Tank)
                    {
                        // Ground units explore
                        ExploreOrDefend(unit, aiPlayer);
                    }
                    else if (unit is AirUnit airUnit)
                    {
                        // Air units patrol
                        if (airUnit.Fuel < airUnit.MaxFuel / 2)
                        {
                            // Low on fuel, return to base
                            ReturnToBase(airUnit, aiPlayer);
                        }
                        else
                        {
                            // Patrol
                            Patrol(airUnit);
                        }
                    }
                }
            }
        }

        private Unit FindNearbyEnemy(Unit unit, Player aiPlayer)
        {
            int searchRadius = 5;

            var tiles = game.Map.GetTilesInRadius(unit.Position, searchRadius);

            foreach (var tile in tiles)
            {
                // Check if this tile is visible to AI
                if (!aiPlayer.FogOfWar.ContainsKey(tile.Position) ||
                    aiPlayer.FogOfWar[tile.Position] != VisibilityLevel.Visible)
                {
                    continue;
                }

                // Check for enemy units
                foreach (var enemyUnit in tile.Units)
                {
                    if (enemyUnit.OwnerId != aiPlayer.PlayerId)
                    {
                        return enemyUnit;
                    }
                }

                // Check for enemy structures
                if (tile.Structure != null && tile.Structure.OwnerId != aiPlayer.PlayerId)
                {
                    // Treat structure as a target (create a dummy unit for targeting)
                    // For now, just skip structures
                }
            }

            return null;
        }

        private bool IsAdjacent(TilePosition pos1, TilePosition pos2)
        {
            int distance = Math.Abs(pos1.X - pos2.X) + Math.Abs(pos1.Y - pos2.Y);
            return distance == 1;
        }

        private void ExecuteCombat(Unit attacker, Unit defender)
        {
            // Simple combat - will implement full combat system later
            // For now, just do damage based on power vs toughness

            int attackDamage = Math.Max(1, attacker.Power - defender.Toughness / 2);
            int counterDamage = Math.Max(1, defender.Power - attacker.Toughness / 2);

            defender.Life -= attackDamage;
            attacker.Life -= counterDamage;

            // Check for kills
            if (defender.Life <= 0)
            {
                // Defender destroyed
                var tile = game.Map.GetTile(defender.Position);
                tile.Units.Remove(defender);

                var defenderOwner = game.Players.FirstOrDefault(p => p.PlayerId == defender.OwnerId);
                defenderOwner?.Units.Remove(defender);

                // Attacker gains experience
                attacker.AddExperience();
            }

            if (attacker.Life <= 0)
            {
                // Attacker destroyed
                var tile = game.Map.GetTile(attacker.Position);
                tile.Units.Remove(attacker);

                var attackerOwner = game.Players.FirstOrDefault(p => p.PlayerId == attacker.OwnerId);
                attackerOwner?.Units.Remove(attacker);
            }

            // Mark attacker as having no movement left
            attacker.MovementPoints = 0;
        }

        private void MoveToward(Unit unit, TilePosition target)
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

                    // No diagonal penalty - terrain cost only

                    if (cost <= movementLeft)
                    {
                        bool occupied = nextTile.Units.Any(u => u.OwnerId == unit.OwnerId);
                        if (occupied)
                            break;

                        var oldTile = game.Map.GetTile(unit.Position);
                        oldTile.Units.Remove(unit);

                        unit.Position = path[currentStep];
                        nextTile.Units.Add(unit);

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
            // Find unexplored areas or move toward enemy bases
            TilePosition targetPos = FindExplorationTarget(unit, aiPlayer);

            if (targetPos.X != -1)
            {
                MoveToward(unit, targetPos);
            }
            else
            {
                // Random walk
                RandomMove(unit);
            }
        }

        private TilePosition FindExplorationTarget(Unit unit, Player aiPlayer)
        {
            // Look for unexplored or explored-but-not-visible tiles
            int searchRadius = 10;

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
            // Move in a random direction
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
            // Find nearest friendly base or carrier
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
                MoveToward(airUnit, nearestBase.Position);
            }
        }

        private void Patrol(AirUnit airUnit)
        {
            // Simple patrol - move in a random direction
            RandomMove(airUnit);
        }
    }
}