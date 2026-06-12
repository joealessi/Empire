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
        }

        public static void Show(Window owner)
        {
            new GameInfoWindow { Owner = owner }.ShowDialog();
        }

        // ── Terrain tab ──────────────────────────────────────────────────────

        private void BuildTerrainTab()
        {
            // Header row
            TerrainPanel.Children.Add(MakeTableHeader(
                "Terrain", "Land Move", "Sea Move", "Air Move", "Defense Bonus", "Notes"));

            // Use real proxy units to call the actual GetMovementCost logic
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

                TerrainPanel.Children.Add(MakeTableRow(alt, $"{icon}  {name}", land, sea, air, def, note));
                alt = !alt;
            }

            // Bridge note
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
            // Section heading
            var header = new TextBlock
            {
                Text = heading,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0xA0, 0xFF)),
                Margin = new Thickness(0, 14, 0, 4)
            };
            UnitsPanel.Children.Add(header);

            UnitsPanel.Children.Add(MakeTableHeader(
                "Unit", "HP", "Atk", "Def", "Move", "Special"));

            bool alt = false;
            foreach (var unit in units)
            {
                string name  = unit.GetType().Name;
                string hp    = unit.MaxLife.ToString();
                string atk   = unit.Attack.ToString();
                string def   = unit.Defense.ToString();
                string move  = unit.MaxMovementPoints.ToString("0.#");
                string notes = GetUnitNotes(unit);

                UnitsPanel.Children.Add(MakeTableRow(alt, name, hp, atk, def, move, notes));
                alt = !alt;
            }
        }

        private static string GetUnitNotes(Unit unit) => unit switch
        {
            Artillery  a => $"Ranged attack (range {a.AttackRange}), no counter-attack",
            Submarine  s => "Can submerge (invisible to enemies)",
            Bomber     b => $"Bombing runs, fuel {b.MaxFuel} tiles",
            Fighter    f => $"Intercepts air units, fuel {f.MaxFuel} tiles",
            Tanker     t => $"Refuels air units in flight, fuel {t.MaxFuel} tiles",
            Transport  t => $"Carries up to {t.Capacity} land units",
            Carrier    c => $"Carries up to {c.Capacity} aircraft",
            Sapper     _ => "Builds bases & bridges, disrupts supply lines",
            Miner      _ => "Builds mines on resource tiles",
            Spy        _ => "Can infiltrate enemy structures",
            AntiAircraft _ => "Bonus attack vs air units",
            OrbitingSatellite o   => $"Vision radius {o.VisionRadius}, orbiting",
            GeosynchronousSatellite g => $"Vision radius {g.VisionRadius}, fixed position",
            _          => ""
        };

        // ── UI helpers ───────────────────────────────────────────────────────

        private static readonly double[] ColWidths = { 160, 42, 42, 42, 48, double.NaN };

        private static UIElement MakeTableHeader(params string[] cols)
        {
            var grid = MakeRowGrid(cols, bold: true,
                bg: Color.FromRgb(0x1A, 0x2C, 0x44),
                fg: Color.FromRgb(0xA0, 0xCF, 0xFF));
            grid.Margin = new Thickness(0, 0, 0, 2);
            return grid;
        }

        private static UIElement MakeTableRow(bool alt, params string[] cols)
        {
            return MakeRowGrid(cols, bold: false,
                bg: alt ? Color.FromRgb(0x12, 0x1A, 0x28) : Color.FromRgb(0x0E, 0x14, 0x22),
                fg: Color.FromRgb(0xE6, 0xED, 0xF5));
        }

        private static Grid MakeRowGrid(string[] cols, bool bold, Color bg, Color fg)
        {
            var grid = new Grid
            {
                Background = new SolidColorBrush(bg),
                Margin     = new Thickness(0, 1, 0, 0)
            };

            for (int i = 0; i < ColWidths.Length; i++)
            {
                grid.ColumnDefinitions.Add(double.IsNaN(ColWidths[i])
                    ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    : new ColumnDefinition { Width = new GridLength(ColWidths[i]) });
            }

            for (int i = 0; i < cols.Length && i < ColWidths.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text            = cols[i],
                    Foreground      = new SolidColorBrush(fg),
                    FontWeight      = bold ? FontWeights.Bold : FontWeights.Normal,
                    FontSize        = 12,
                    Padding         = new Thickness(6, 4, 4, 4),
                    TextWrapping    = TextWrapping.Wrap,
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
                Text       = text,
                Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0xA1, 0xB5)),
                FontSize   = 11,
                FontStyle  = FontStyles.Italic,
                Margin     = new Thickness(4, 8, 4, 0),
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
