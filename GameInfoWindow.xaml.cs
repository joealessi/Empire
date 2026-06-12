using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EmpireGame
{
    public partial class GameInfoWindow : Window
    {
        public GameInfoWindow()
        {
            InitializeComponent();
            BuildTerrainTab();
            BuildUnitsTab();
            BuildResourcesTab();
            BuildCivicsTab();
        }

        public static void Show(Window owner)
        {
            new GameInfoWindow { Owner = owner }.ShowDialog();
        }

        // ── Terrain tab ──────────────────────────────────────────────────────

        private void BuildTerrainTab()
        {
            TerrainPanel.Children.Add(MakeTableHeader(TerrainCols,
                "Terrain", "Land Move", "Sea Move", "Air Move", "Defense Bonus", "Notes"));

            var landProxy = new Army();
            var seaProxy  = new Destroyer();
            var airProxy  = new Fighter();

            var terrains = new[]
            {
                (TerrainType.Plains,       "🌾", "Plains"),
                (TerrainType.Land,         "🟫", "Land"),
                (TerrainType.Forest,       "🌲", "Forest"),
                (TerrainType.Hills,        "⛰",  "Hills"),
                (TerrainType.Mountain,     "🏔",  "Mountain"),
                (TerrainType.CoastalWater, "🌊", "Coastal Water"),
                (TerrainType.Ocean,        "🔵", "Ocean"),
            };

            bool alt = false;
            foreach (var (type, icon, name) in terrains)
            {
                var tile = new Tile(new TilePosition(0, 0), type);
                string land = MoveCostStr(tile.GetMovementCost(landProxy));
                string sea  = MoveCostStr(tile.GetMovementCost(seaProxy));
                string air  = MoveCostStr(tile.GetMovementCost(airProxy));
                string def  = DefBonusStr(type);
                string note = type switch
                {
                    TerrainType.CoastalWater => "Sea & air only (bridges allow land)",
                    TerrainType.Ocean        => "Sea & air only",
                    TerrainType.Mountain     => "Blocks line of sight",
                    _                        => ""
                };
                TerrainPanel.Children.Add(MakeTableRow(TerrainCols, alt, $"{icon}  {name}", land, sea, air, def, note));
                alt = !alt;
            }
            TerrainPanel.Children.Add(MakeNote("🌉  A Bridge over water costs land units 1 movement point to cross."));
        }

        private static string MoveCostStr(double cost)
            => cost >= double.MaxValue / 2 ? "✗" : cost.ToString("0.#");

        private static string DefBonusStr(TerrainType t) => t switch
        {
            TerrainType.Mountain     => "+10",
            TerrainType.Hills        => "+5",
            TerrainType.Forest       => "+3",
            TerrainType.CoastalWater => "+2",
            _                        => "—"
        };

        // ── Units tab ────────────────────────────────────────────────────────

        private void BuildUnitsTab()
        {
            BuildUnitSection("🪖  Land Units", new Unit[]
            {
                new Army(), new Tank(), new Artillery(), new AntiAircraft(),
                new Spy(), new Sapper(), new Miner()
            });
            BuildUnitSection("⚓  Sea Units", new Unit[]
            {
                new PatrolBoat(), new Destroyer(), new Submarine(),
                new Battleship(), new Transport(), new Carrier()
            });
            BuildUnitSection("✈  Air Units", new Unit[]
            {
                new Fighter(), new Bomber(), new Tanker()
            });
            BuildUnitSection("🛰  Satellites", new Unit[]
            {
                new OrbitingSatellite(), new GeosynchronousSatellite()
            });
        }

        private void BuildUnitSection(string heading, Unit[] units)
        {
            UnitsPanel.Children.Add(MakeSectionHeader(heading));
            UnitsPanel.Children.Add(MakeTableHeader(UnitCols, "Unit", "HP", "Atk", "Def", "Move", "Special"));

            bool alt = false;
            foreach (var unit in units)
            {
                UnitsPanel.Children.Add(MakeTableRow(UnitCols, alt,
                    unit.GetType().Name,
                    unit.MaxLife.ToString(),
                    unit.Attack.ToString(),
                    unit.Defense.ToString(),
                    unit.MaxMovementPoints.ToString("0.#"),
                    GetUnitNotes(unit)));
                alt = !alt;
            }
        }

        private static string GetUnitNotes(Unit unit) => unit switch
        {
            Artillery  a => $"Ranged attack (range {a.AttackRange}), no counter-attack",
            Submarine  _  => "Can submerge (invisible to enemies) — requires 1 ☢️",
            Bomber     b => $"Bombing runs, fuel {b.MaxFuel} tiles",
            Fighter    f => $"Intercepts air units, fuel {f.MaxFuel} tiles",
            Tanker     t => $"Refuels air units in flight, fuel {t.MaxFuel} tiles",
            Transport  t => $"Carries up to {t.Capacity} land units",
            Carrier    c => $"Carries up to {c.Capacity} aircraft",
            Sapper     _ => "Builds bases & bridges, disrupts supply lines",
            Miner      _ => "Builds mines on resource tiles",
            Spy        _ => "Can infiltrate enemy structures",
            AntiAircraft _ => "Bonus attack vs air units",
            OrbitingSatellite o   => $"Vision radius {o.VisionRadius}, orbiting — requires 1 ☢️",
            GeosynchronousSatellite g => $"Vision radius {g.VisionRadius}, fixed position — requires 1 ☢️",
            _          => ""
        };

        // ── Resources tab ────────────────────────────────────────────────────

        private void BuildResourcesTab()
        {
            ResourcesPanel.Children.Add(MakeTableHeader(ResCols,
                "Resource", "Symbol", "Yield / Mine", "Spawns On", "Mine HP", "Notes"));

            // Gold first — special case (not mined)
            ResourcesPanel.Children.Add(MakeTableRow(ResCols, false,
                "💰  Gold", "💰", "—",
                "Cities (+3/turn), Bases (+1/turn)", "—",
                "Primary currency. Treasury upgrade adds +1/turn per structure."));

            bool alt = true;
            foreach (var def in ResourceRegistry.Definitions.Where(d => d.IsMineable))
            {
                string terrain = def.AllowedTerrain.Count > 0
                    ? string.Join(", ", def.AllowedTerrain.Select(t => t.ToString()))
                    : "—";
                string yield = def.YieldPerTurn == Math.Floor(def.YieldPerTurn)
                    ? $"+{(int)def.YieldPerTurn}/turn"
                    : $"+{def.YieldPerTurn:0.##}/turn";
                string note = def.Type == ResourceType.Uranium
                    ? "☢️ Hidden until High Technology civic. Required by Submarines & Satellites."
                    : def.Type == ResourceType.Oil
                        ? "Required by naval & air units. Also used for High Technology civic."
                        : "Required by armoured & industrial units.";

                ResourcesPanel.Children.Add(MakeTableRow(ResCols, alt,
                    $"{def.Symbol}  {def.DisplayName}",
                    def.Symbol,
                    yield,
                    terrain,
                    def.MineHp.ToString(),
                    note));
                alt = !alt;
            }

            ResourcesPanel.Children.Add(MakeNote("⛏️  A Miner unit builds mines. Mines must be connected by a supply line to a friendly Base or City to produce income."));
            ResourcesPanel.Children.Add(MakeNote("☢️  Uranium yields 0.25/turn — accumulates over 4 turns before adding 1 to your stockpile."));

            // Production costs summary
            ResourcesPanel.Children.Add(MakeSectionHeader("💸  Unit Production Costs"));
            ResourcesPanel.Children.Add(MakeTableHeader(CostCols, "Unit", "💰 Gold", "⚙️ Steel", "🛢️ Oil", "☢️ Uranium"));

            bool ca = false;
            foreach (var kv in UnitProductionOrder.Costs.OrderBy(k => k.Key.Name))
            {
                var cost = kv.Value;
                int gold    = cost.TryGetValue(ResourceType.Gold,    out var g) ? g : 0;
                int steel   = cost.TryGetValue(ResourceType.Steel,   out var s) ? s : 0;
                int oil     = cost.TryGetValue(ResourceType.Oil,     out var o) ? o : 0;
                int uranium = cost.TryGetValue(ResourceType.Uranium, out var u) ? u : 0;

                ResourcesPanel.Children.Add(MakeTableRow(CostCols, ca,
                    kv.Key.Name,
                    gold > 0    ? gold.ToString()    : "—",
                    steel > 0   ? steel.ToString()   : "—",
                    oil > 0     ? oil.ToString()     : "—",
                    uranium > 0 ? uranium.ToString() : "—"));
                ca = !ca;
            }
        }

        // ── Civics tab ───────────────────────────────────────────────────────

        private void BuildCivicsTab()
        {
            // Per-structure upgrades
            CivicsPanel.Children.Add(MakeSectionHeader("🏛️  Per-Structure Upgrades"));
            CivicsPanel.Children.Add(MakeNote("Bought with populace at any owned City or Base. Effects apply only to that structure."));
            CivicsPanel.Children.Add(MakeTableHeader(CivicCols, "Upgrade", "Cost", "Effect"));

            var structureUpgrades = new[]
            {
                ("🏭  Industry",    $"{CivicUpgrades.CostIndustry} 👥",    "+1.25% production per turn"),
                ("🧱  Fortify",     $"{CivicUpgrades.CostFortify} 👥",     "+5 max structure HP"),
                ("🗼  Watchtower",  $"{CivicUpgrades.CostWatchtower} 👥",  "+1 tile vision range"),
                ("🏘️  Housing",    $"{CivicUpgrades.CostHousing} 👥",     "+0.5 populace growth per turn"),
                ("🏦  Treasury",   $"{CivicUpgrades.CostTreasury} 👥",    "+1 gold income per turn"),
                ("🔧  Repair",     $"{CivicUpgrades.CostRepair} 👥",      "Instantly restore structure to full HP"),
                ("🪖  Conscript",  $"{CivicUpgrades.CostConscript} 👥",   "Immediately deploy 1 Army to an adjacent tile"),
            };

            bool alt = false;
            foreach (var (name, cost, effect) in structureUpgrades)
            {
                CivicsPanel.Children.Add(MakeTableRow(CivicCols, alt, name, cost, effect));
                alt = !alt;
            }

            // Player-wide upgrades
            CivicsPanel.Children.Add(MakeSectionHeader("🌐  Player-Wide Upgrades"));
            CivicsPanel.Children.Add(MakeNote("Bought at any owned City or Base. Effect applies globally to all your units or structures."));
            CivicsPanel.Children.Add(MakeTableHeader(CivicCols, "Upgrade", "Cost", "Effect"));

            var playerUpgrades = new[]
            {
                ("🎖️  Military I",
                 $"{CivicUpgrades.CostMilitary1} 👥",
                 "+1 HP to all Armies (current and future). Prerequisite for Military II."),
                ("🎖️  Military II",
                 $"{CivicUpgrades.CostMilitary2} 👥 + {CivicUpgrades.SteelCostMilitary2} ⚙️",
                 "+1 HP to all Tanks (current and future). Requires Military I."),
                ("☢️  High Technology",
                 $"{CivicUpgrades.CostHighTechnology} 👥 + {CivicUpgrades.OilCostHighTechnology} 🛢️",
                 "Reveals uranium deposits on the map. Unlocks uranium mine construction. Required to build Submarines & Satellites."),
            };

            alt = false;
            foreach (var (name, cost, effect) in playerUpgrades)
            {
                CivicsPanel.Children.Add(MakeTableRow(CivicCols, alt, name, cost, effect));
                alt = !alt;
            }

            CivicsPanel.Children.Add(MakeNote("👥  Populace grows naturally each turn (City +2/turn, Base +1/turn). Housing upgrade adds +0.5. Populace can never drop below 1."));
        }

        // ── Column width presets ─────────────────────────────────────────────

        private static readonly double[] TerrainCols = { 150, 72, 72, 72, 90, double.NaN };
        private static readonly double[] UnitCols    = { 160, 42, 42, 42, 48, double.NaN };
        private static readonly double[] ResCols     = { 120, 48, 90, 140, 60, double.NaN };
        private static readonly double[] CostCols    = { 180, 56, 60, 56, 70 };
        private static readonly double[] CivicCols   = { 160, 160, double.NaN };

        // ── UI helpers ───────────────────────────────────────────────────────

        private static UIElement MakeSectionHeader(string text)
        {
            return new TextBlock
            {
                Text       = text,
                FontSize   = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0xA0, 0xFF)),
                Margin     = new Thickness(0, 14, 0, 4)
            };
        }

        private static UIElement MakeTableHeader(double[] cols, params string[] labels)
        {
            var grid = MakeRowGrid(cols, labels, bold: true,
                bg: Color.FromRgb(0x1A, 0x2C, 0x44),
                fg: Color.FromRgb(0xA0, 0xCF, 0xFF));
            grid.Margin = new Thickness(0, 0, 0, 2);
            return grid;
        }

        private static UIElement MakeTableRow(double[] cols, bool alt, params string[] values)
        {
            return MakeRowGrid(cols, values, bold: false,
                bg: alt ? Color.FromRgb(0x12, 0x1A, 0x28) : Color.FromRgb(0x0E, 0x14, 0x22),
                fg: Color.FromRgb(0xE6, 0xED, 0xF5));
        }

        private static Grid MakeRowGrid(double[] colWidths, string[] cols, bool bold, Color bg, Color fg)
        {
            var grid = new Grid
            {
                Background = new SolidColorBrush(bg),
                Margin     = new Thickness(0, 1, 0, 0)
            };

            foreach (var w in colWidths)
            {
                grid.ColumnDefinitions.Add(double.IsNaN(w)
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(w) });
            }

            for (int i = 0; i < cols.Length && i < colWidths.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text              = cols[i],
                    Foreground        = new SolidColorBrush(fg),
                    FontWeight        = bold ? FontWeights.Bold : FontWeights.Normal,
                    FontSize          = 12,
                    Padding           = new Thickness(6, 4, 4, 4),
                    TextWrapping      = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }
            return grid;
        }

        private static UIElement MakeNote(string text)
        {
            return new TextBlock
            {
                Text         = text,
                Foreground   = new SolidColorBrush(Color.FromRgb(0x93, 0xA1, 0xB5)),
                FontSize     = 11,
                FontStyle    = FontStyles.Italic,
                Margin       = new Thickness(4, 8, 4, 0),
                TextWrapping = TextWrapping.Wrap
            };
        }

        // ── Window chrome ────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}