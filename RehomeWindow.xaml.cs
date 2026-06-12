using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EmpireGame
{
    public partial class RehomeWindow : Window
    {
        private readonly Structure _source;
        private readonly Game _game;
        private readonly Action<TilePosition> _locateCallback;

        // Parallel lists for UnitsList items
        private readonly List<Unit> _units = new List<Unit>();
        // Parallel list for DestinationList items
        private readonly List<Structure> _destinations = new List<Structure>();

        public RehomeWindow(Structure source, Game game, Action<TilePosition> locateCallback)
        {
            InitializeComponent();
            _source = source;
            _game = game;
            _locateCallback = locateCallback;

            SubtitleText.Text = $"from {source.GetName()}";
            TitleText.Text = $"♻ REHOME UNITS — {source.GetName()}";

            PopulateUnits();
            PopulateDestinations();
            UpdateRehomeButton();
        }

        private void PopulateUnits()
        {
            _units.Clear();
            UnitsList.Items.Clear();

            void AddUnits(IEnumerable<Unit> units, string location)
            {
                foreach (var u in units)
                {
                    _units.Add(u);
                    UnitsList.Items.Add($"{u.GetName()}  [{location}]  Life:{u.Life}/{u.MaxLife}");
                }
            }

            if (_source is Base b)
            {
                AddUnits(b.Barracks.Cast<Unit>(), "Barracks");
                AddUnits(b.Airport.Cast<Unit>(), "Airport");
                AddUnits(b.MotorPool, "Motor Pool");
            }
            else if (_source is City c)
            {
                AddUnits(c.Barracks.Cast<Unit>(), "Barracks");
                AddUnits(c.Airport.Cast<Unit>(), "Airport");
                AddUnits(c.MotorPool, "Motor Pool");
            }
        }

        private void PopulateDestinations()
        {
            _destinations.Clear();
            DestinationList.Items.Clear();

            var owner = _game.Players.FirstOrDefault(p => p.PlayerId == _source.OwnerId);
            if (owner == null) return;

            foreach (var s in owner.Structures.OrderBy(s => s.GetName()))
            {
                if (s == _source) continue;
                if (!(s is Base || s is City)) continue;

                int dist = Math.Abs(s.Position.X - _source.Position.X) +
                           Math.Abs(s.Position.Y - _source.Position.Y);

                string airNote = "";
                if (s is Base baseS)
                {
                    int airCap = Base.MAX_AIRPORT_CAPACITY - baseS.Airport.Count;
                    airNote = airCap > 0 ? $"  ✈{airCap}" : "  ✈FULL";
                }
                else if (s is City cityS)
                {
                    int airCap = City.MAX_AIRPORT_CAPACITY - cityS.Airport.Count;
                    airNote = airCap > 0 ? $"  ✈{airCap}" : "  ✈FULL";
                }

                _destinations.Add(s);
                DestinationList.Items.Add($"{s.GetName()}  [{(s is City ? "City" : "Base")}]{airNote}  ~{dist} tiles");
            }
        }

        private void DestinationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRehomeButton();

            int idx = DestinationList.SelectedIndex;
            if (idx >= 0 && idx < _destinations.Count)
                _locateCallback?.Invoke(_destinations[idx].Position);
        }

        private void UnitsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRehomeButton();
        }

        private void UpdateRehomeButton()
        {
            bool hasUnits = UnitsList.SelectedItems.Count > 0;
            bool hasDest = DestinationList.SelectedIndex >= 0;

            if (!hasUnits || !hasDest)
            {
                RehomeButton.IsEnabled = false;
                DestinationHintText.Text = "";
                return;
            }

            var dest = _destinations[DestinationList.SelectedIndex];

            // Check if any selected air units are going to a full airport
            var selectedUnits = UnitsList.SelectedItems.Cast<object>()
                .Select(i => _units[UnitsList.Items.IndexOf(i)])
                .ToList();

            bool hasAir = selectedUnits.Any(u => u is AirUnit);
            int airSlots = dest is Base db ? Base.MAX_AIRPORT_CAPACITY - db.Airport.Count
                         : dest is City dc ? City.MAX_AIRPORT_CAPACITY - dc.Airport.Count
                         : 0;
            int airCount = selectedUnits.Count(u => u is AirUnit);

            if (hasAir && airSlots <= 0)
            {
                RehomeButton.IsEnabled = false;
                DestinationHintText.Text = $"⚠ {dest.GetName()} airport is full — cannot send air units there.";
                return;
            }
            if (hasAir && airCount > airSlots)
            {
                RehomeButton.IsEnabled = false;
                DestinationHintText.Text = $"⚠ {dest.GetName()} only has {airSlots} airport slot(s) — deselect some air units.";
                return;
            }

            int dist = Math.Abs(dest.Position.X - _source.Position.X)
                     + Math.Abs(dest.Position.Y - _source.Position.Y);
            int turns = Math.Max(1, dist / 10);
            string turnWord = turns == 1 ? "turn" : "turns";
            DestinationHintText.Text = $"✓ {selectedUnits.Count} unit(s) → {dest.GetName()} — arrives in {turns} {turnWord}";
            RehomeButton.IsEnabled = true;
        }

        private void RehomeButton_Click(object sender, RoutedEventArgs e)
        {
            int destIdx = DestinationList.SelectedIndex;
            if (destIdx < 0 || destIdx >= _destinations.Count) return;
            var dest = _destinations[destIdx];

            var selected = UnitsList.SelectedItems.Cast<object>()
                .Select(i => _units[UnitsList.Items.IndexOf(i)])
                .ToList();

            int dist = Math.Abs(dest.Position.X - _source.Position.X)
                     + Math.Abs(dest.Position.Y - _source.Position.Y);
            int turns = Math.Max(1, dist / 10);

            // Remove units from source now; they'll arrive at destination in 'turns' turns
            foreach (var unit in selected)
                RemoveFromSource(unit);

            var player = _game.Players.FirstOrDefault(p => p.PlayerId == _source.OwnerId);
            player?.RehomeTransitOrders.Add(new RehomeTransitOrder(selected, _source, dest, turns));

            _turnsToArrive = turns;
            DialogResult = true;
            Close();
        }

        // Exposed so the caller can include it in the log message
        public int TransitTurns => _turnsToArrive;
        private int _turnsToArrive;

        private void RemoveFromSource(Unit unit)
        {
            if (_source is Base sb)
            {
                if (unit is LandUnit lu) sb.Barracks.Remove(lu);
                else if (unit is AirUnit au) sb.Airport.Remove(au);
                else sb.MotorPool.Remove(unit);
            }
            else if (_source is City sc)
            {
                if (unit is LandUnit lu) sc.Barracks.Remove(lu);
                else if (unit is AirUnit au) sc.Airport.Remove(au);
                else sc.MotorPool.Remove(unit);
            }
        }

        private void AddToDestination(Unit unit, Structure dest)
        {
            if (unit is AirUnit airUnit)
            {
                if (dest is Base db) { db.Airport.Add(airUnit); airUnit.HomeBaseId = db.StructureId; airUnit.Fuel = airUnit.MaxFuel; }
                else if (dest is City dc) { dc.Airport.Add(airUnit); airUnit.HomeBaseId = dc.StructureId; airUnit.Fuel = airUnit.MaxFuel; }
            }
            else if (unit is LandUnit landUnit)
            {
                if (dest is Base db && db.Barracks.Count < Base.MAX_BARRACKS_CAPACITY) { db.Barracks.Add(landUnit); }
                else if (dest is City dc && dc.Barracks.Count < City.MAX_BARRACKS_CAPACITY) { dc.Barracks.Add(landUnit); }
            }
            else
            {
                if (dest is Base db) db.MotorPool.Add(unit);
                else if (dest is City dc) dc.MotorPool.Add(unit);
            }

            unit.Position = dest.Position;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
