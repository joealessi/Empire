using System.Collections.Generic;

namespace EmpireGame
{
    /// <summary>
    /// Tracks a group of units being administratively transferred between structures.
    /// Units are removed from the source immediately; they arrive at the destination
    /// after TurnsRemaining turns have elapsed.
    /// </summary>
    public class RehomeTransitOrder
    {
        public List<Unit> Units { get; set; } = new List<Unit>();
        public Structure Source { get; set; }
        public Structure Destination { get; set; }
        public int TurnsRemaining { get; set; }
        public int TotalTurns { get; set; }

        public RehomeTransitOrder(List<Unit> units, Structure source, Structure destination, int turns)
        {
            Units = new List<Unit>(units);
            Source = source;
            Destination = destination;
            TurnsRemaining = turns;
            TotalTurns = turns;
        }
    }
}
