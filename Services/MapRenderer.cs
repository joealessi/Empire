using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmpireGame
{
    public class MapRenderer
    {
        private Game game;
        private int tileSize;
        private WriteableBitmap bitmap;
        private Unit selectedUnit;
        private Structure selectedStructure;
        private double iconScale;

        // Resource icon bitmaps
        private BitmapSource oilIcon;
        private BitmapSource steelIcon;

        // Color definitions for terrain
        private static readonly Color OceanColor = Color.FromRgb(20, 60, 120);
        private static readonly Color CoastalWaterColor = Color.FromRgb(40, 100, 160);
        private static readonly Color LandColor = Color.FromRgb(100, 140, 80);
        private static readonly Color PlainsColor = Color.FromRgb(120, 160, 90);
        private static readonly Color ForestColor = Color.FromRgb(40, 80, 40);
        private static readonly Color HillsColor = Color.FromRgb(120, 100, 70);
        private static readonly Color MountainColor = Color.FromRgb(140, 140, 140);
        private static readonly Color FogColor = Color.FromRgb(60, 60, 60);

        // Player colors
        private static readonly Color[] PlayerColors = new Color[]
        {
            Color.FromRgb(0, 120, 255),      // Player 0 - Blue
            Color.FromRgb(255, 60, 60),      // Player 1 - Red
            Color.FromRgb(60, 255, 60),      // Player 2 - Green
            Color.FromRgb(255, 255, 60),     // Player 3 - Yellow
            Color.FromRgb(255, 140, 0),      // Player 4 - Orange
            Color.FromRgb(160, 60, 255),     // Player 5 - Purple
            Color.FromRgb(0, 255, 255),      // Player 6 - Cyan
            Color.FromRgb(255, 180, 200)     // Player 7 - Pink
        };

        public MapRenderer(Game game, int tileSize)
        {
            this.game = game;
            this.tileSize = tileSize;
            this.iconScale = tileSize / 16.0;

            int width = game.Map.Width * tileSize;
            int height = game.Map.Height * tileSize;

            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);

            // Load resource icons
            try
            {
                oilIcon = new BitmapImage(new Uri("pack://application:,,,/Resources/oil_16.png"));
                steelIcon = new BitmapImage(new Uri("pack://application:,,,/Resources/steel_16.png"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load resource icons: {ex.Message}");
            }
        }

        public WriteableBitmap RenderMap(Player currentPlayer, Unit selectedUnit = null, Structure selectedStructure = null)
        {
            this.selectedUnit = selectedUnit;
            this.selectedStructure = selectedStructure;

            bitmap.Lock();

            try
            {
                unsafe
                {
                    IntPtr pBackBuffer = bitmap.BackBuffer;
                    int stride = bitmap.BackBufferStride;

                    for (int x = 0; x < game.Map.Width; x++)
                    {
                        for (int y = 0; y < game.Map.Height; y++)
                        {
                            var tilePos = new TilePosition(x, y);
                            var tile = game.Map.GetTile(tilePos);

                            // Check fog of war
                            VisibilityLevel visibility = VisibilityLevel.Hidden;
                            if (currentPlayer.FogOfWar.ContainsKey(tilePos))
                            {
                                visibility = currentPlayer.FogOfWar[tilePos];
                            }

                            // Render tile
                            RenderTile(pBackBuffer, stride, x, y, tile, visibility);

                            // Render contents if visible
                            if (visibility == VisibilityLevel.Visible)
                            {
                                // Render structure
                                if (tile.Structure != null)
                                {
                                    RenderStructure(pBackBuffer, stride, x, y, tile.Structure);
                                }

                                // Render units
                                if (tile.Units.Count > 0)
                                {
                                    RenderUnit(pBackBuffer, stride, x, y, tile.Units[0]);
                                }
                            }

                            // Highlight selected unit or structure
                            if (selectedUnit != null && selectedUnit.Position.Equals(tilePos))
                            {
                                DrawHighlight(pBackBuffer, stride, x * tileSize, y * tileSize, Color.FromRgb(255, 255, 0));
                            }
                            else if (selectedStructure != null && selectedStructure.Position.Equals(tilePos))
                            {
                                DrawHighlight(pBackBuffer, stride, x * tileSize, y * tileSize, Color.FromRgb(255, 255, 0));
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                bitmap.Unlock();
            }

            return bitmap;
        }

        private unsafe void RenderTile(IntPtr pBackBuffer, int stride, int tileX, int tileY, Tile tile, VisibilityLevel visibility)
        {
            Color baseColor;

            if (visibility == VisibilityLevel.Hidden)
            {
                baseColor = Colors.Black;
            }
            else
            {
                baseColor = GetTerrainColor(tile.Terrain);

                if (visibility == VisibilityLevel.Explored)
                {
                    baseColor = Color.FromRgb(
                        (byte)(baseColor.R * 0.5),
                        (byte)(baseColor.G * 0.5),
                        (byte)(baseColor.B * 0.5)
                    );
                }
            }

            for (int py = 0; py < tileSize; py++)
            {
                for (int px = 0; px < tileSize; px++)
                {
                    int screenX = tileX * tileSize + px;
                    int screenY = tileY * tileSize + py;

                    byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                    pixel[0] = baseColor.B;
                    pixel[1] = baseColor.G;
                    pixel[2] = baseColor.R;
                    pixel[3] = 255;
                }
            }

            // Render resource icons BEFORE units (so units render on top)
            if (visibility == VisibilityLevel.Visible && tile.Resource != ResourceType.None)
            {
                RenderResourceIcon(pBackBuffer, stride, tileX, tileY, tile.Resource);
            }

            if (visibility == VisibilityLevel.Visible && tile.OwnerId >= 0 && tile.Resource != ResourceType.None)
            {
                Color ownerColor = GetPlayerColor(tile.OwnerId);
                // Draw a thicker border for owned resource tiles
                DrawBorder(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, tileSize, tileSize, ownerColor);
            }
            else
                DrawBorder(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, tileSize, tileSize, Color.FromRgb(80, 80, 80));
        }

        private unsafe void RenderResourceIcon(IntPtr pBackBuffer, int stride, int tileX, int tileY, ResourceType resource)
        {
            BitmapSource icon = resource == ResourceType.Oil ? oilIcon : steelIcon;

            if (icon == null)
                return;

            // Calculate position (top-right corner of tile)
            int iconSize = 16; // Icons are 16x16
            int destX = tileX * tileSize + tileSize - iconSize - 2;
            int destY = tileY * tileSize + 2;

            // Convert icon to Bgr32 format if needed
            BitmapSource convertedIcon = icon;
            if (icon.Format != PixelFormats.Bgr32)
            {
                convertedIcon = new FormatConvertedBitmap(icon, PixelFormats.Bgr32, null, 0);
            }

            // Get icon pixel data
            int iconStride = iconSize * 4;
            byte[] iconPixels = new byte[iconStride * iconSize];
            convertedIcon.CopyPixels(iconPixels, iconStride, 0);

            // Copy icon pixels to the main bitmap
            for (int y = 0; y < iconSize; y++)
            {
                for (int x = 0; x < iconSize; x++)
                {
                    int screenX = destX + x;
                    int screenY = destY + y;

                    // Check bounds
                    if (screenX < 0 || screenX >= bitmap.PixelWidth ||
                        screenY < 0 || screenY >= bitmap.PixelHeight)
                        continue;

                    // Get icon pixel
                    int iconOffset = y * iconStride + x * 4;
                    byte iconB = iconPixels[iconOffset];
                    byte iconG = iconPixels[iconOffset + 1];
                    byte iconR = iconPixels[iconOffset + 2];
                    byte iconA = iconPixels[iconOffset + 3];

                    // Skip fully transparent pixels
                    if (iconA == 0)
                        continue;

                    // Write to destination
                    byte* destPixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;

                    if (iconA == 255)
                    {
                        // Fully opaque - just copy
                        destPixel[0] = iconB;
                        destPixel[1] = iconG;
                        destPixel[2] = iconR;
                        destPixel[3] = 255;
                    }
                    else
                    {
                        // Alpha blend
                        float alpha = iconA / 255.0f;
                        destPixel[0] = (byte)(iconB * alpha + destPixel[0] * (1 - alpha));
                        destPixel[1] = (byte)(iconG * alpha + destPixel[1] * (1 - alpha));
                        destPixel[2] = (byte)(iconR * alpha + destPixel[2] * (1 - alpha));
                        destPixel[3] = 255;
                    }
                }
            }
        }

        private unsafe void RenderStructure(IntPtr pBackBuffer, int stride, int tileX, int tileY, Structure structure)
        {
            Color color = GetPlayerColor(structure.OwnerId);

            int margin = (int)(2 * iconScale);
            int startX = tileX * tileSize + margin;
            int startY = tileY * tileSize + margin;
            int size = tileSize - margin * 2;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int screenX = startX + px;
                    int screenY = startY + py;

                    byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                    pixel[0] = color.B;
                    pixel[1] = color.G;
                    pixel[2] = color.R;
                    pixel[3] = 255;
                }
            }

            if (structure is Base)
            {
                DrawBaseIcon(pBackBuffer, stride, tileX * tileSize, tileY * tileSize);
            }
            else if (structure is City)
            {
                DrawCityIcon(pBackBuffer, stride, tileX * tileSize, tileY * tileSize);
            }
        }

        private unsafe void RenderUnit(IntPtr pBackBuffer, int stride, int tileX, int tileY, Unit unit)
        {
            // Don't render enemy units in fog of war
            Player currentPlayer = game.CurrentPlayer;
            var tilePos = new TilePosition(tileX, tileY);

            // Check if this tile is visible to current player
            if (!currentPlayer.FogOfWar.ContainsKey(tilePos) ||
                currentPlayer.FogOfWar[tilePos] != VisibilityLevel.Visible)
            {
                return;
            }

            Color color = GetPlayerColor(unit.OwnerId);

            // Draw background shape
            if (unit is AirUnit)
            {
                DrawTriangle(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, color);
            }
            else if (unit is SeaUnit)
            {
                DrawDiamond(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, color);
            }
            else
            {
                DrawCircle(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, color);
            }

            // Draw the unit-specific icon
            int startX = tileX * tileSize;
            int startY = tileY * tileSize;

            // Check if spy is disguised
            if (unit is Spy spy && !spy.IsRevealed && unit.OwnerId != currentPlayer.PlayerId)
            {
                DrawArmyIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Army)
            {
                DrawArmyIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Tank)
            {
                DrawTankIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Artillery)
            {
                DrawArtilleryIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is AntiAircraft)
            {
                DrawAntiAircraftIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Fighter)
            {
                DrawFighterIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Bomber)
            {
                DrawBomberIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Tanker)
            {
                DrawTankerIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Carrier)
            {
                DrawCarrierIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Battleship)
            {
                DrawBattleshipIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Destroyer)
            {
                DrawDestroyerIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Submarine)
            {
                DrawSubmarineIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is PatrolBoat)
            {
                DrawPatrolBoatIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Transport)
            {
                DrawTransportIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
            else if (unit is Spy)
            {
                DrawSpyIcon(pBackBuffer, stride, startX, startY, unit.IsVeteran);
            }
        }

        // Icon drawing methods for each unit type

        private unsafe void DrawArmyIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale); // Scale factor, minimum 1

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Head (circle) - scaled
            for (int dy = -s; dy <= s; dy++)
            {
                for (int dx = -s; dx <= s; dx++)
                {
                    if (dx * dx + dy * dy <= s * s)
                        DrawPixel(pBackBuffer, stride, cx + dx, cy - 3 * s + dy, color);
                }
            }

            // Body (vertical line) - scaled
            for (int i = -2 * s; i <= 2 * s; i++)
                DrawPixel(pBackBuffer, stride, cx, cy + i, color);

            // Arms (horizontal line) - scaled
            for (int x = -2 * s; x <= 2 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy - s, color);

            // Legs - scaled
            for (int i = 0; i <= s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx - s, cy + 3 * s + i, color);
                DrawPixel(pBackBuffer, stride, cx + s, cy + 3 * s + i, color);
            }
        }

        private unsafe void DrawTankIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Body (rectangle) - scaled
            for (int x = -3 * s; x <= 3 * s; x++)
            {
                for (int y = -s; y <= 2 * s; y++)
                {
                    DrawPixel(pBackBuffer, stride, cx + x, cy + y, color);
                }
            }

            // Barrel (extending right) - scaled
            for (int x = 3 * s; x <= 5 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);
        }

        private unsafe void DrawArtilleryIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Barrel (angled line) - scaled
            for (int i = 0; i <= 4 * s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx + i / s, cy - i / s, color);
            }

            // Wheels - scaled
            for (int i = 0; i <= s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx - 2 * s + i, cy + 2 * s, color);
                DrawPixel(pBackBuffer, stride, cx + 2 * s + i, cy + 2 * s, color);
            }
        }

        private unsafe void DrawAntiAircraftIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Barrel (vertical) - scaled
            for (int y = -4 * s; y <= s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);

            // Base (horizontal) - scaled
            for (int x = -2 * s; x <= 2 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy + 2 * s, color);
        }

        private unsafe void DrawFighterIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Fuselage (vertical) - scaled
            for (int y = -4 * s; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);

            // Wings (horizontal) - scaled
            for (int x = -3 * s; x <= 3 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);

            // Tail fins - scaled
            DrawPixel(pBackBuffer, stride, cx - s, cy + 3 * s, color);
            DrawPixel(pBackBuffer, stride, cx + s, cy + 3 * s, color);
        }

        private unsafe void DrawBomberIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Fuselage (thicker vertical) - scaled
            for (int y = -3 * s; y <= 3 * s; y++)
            {
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);
                DrawPixel(pBackBuffer, stride, cx + s, cy + y, color);
            }

            // Wide wings - scaled
            for (int x = -4 * s; x <= 4 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);
        }

        private unsafe void DrawTankerIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Fuselage - scaled
            for (int y = -3 * s; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);

            // Wings - scaled
            for (int x = -3 * s; x <= 3 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);

            // Fuel tanks under wings - scaled
            for (int i = 0; i <= s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx - 2 * s, cy + s + i, color);
                DrawPixel(pBackBuffer, stride, cx + 2 * s, cy + s + i, color);
            }
        }

        private unsafe void DrawSubmarineIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Hull (horizontal oval) - scaled
            for (int x = -4 * s; x <= 4 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);
            }
            for (int x = -3 * s; x <= 3 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy - s, color);
                DrawPixel(pBackBuffer, stride, cx + x, cy + s, color);
            }

            // Periscope - scaled
            for (int y = -3 * s; y <= -2 * s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);
        }

        private unsafe void DrawDestroyerIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Hull - scaled
            for (int x = -4 * s; x <= 3 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy + s, color);
            }

            // Bow (pointed) - scaled
            DrawPixel(pBackBuffer, stride, cx + 4 * s, cy, color);

            // Superstructure - scaled
            for (int y = -s; y <= 0; y++)
            {
                DrawPixel(pBackBuffer, stride, cx - s, cy + y, color);
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);
            }
        }

        private unsafe void DrawCarrierIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Deck (long flat rectangle) - scaled
            for (int x = -5 * s; x <= 4 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy - s, color);
                DrawPixel(pBackBuffer, stride, cx + x, cy, color);
            }

            // Island (small superstructure) - scaled
            for (int y = -3 * s; y <= -2 * s; y++)
                DrawPixel(pBackBuffer, stride, cx - 2 * s, cy + y, color);
        }

        private unsafe void DrawBattleshipIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Hull - scaled
            for (int x = -4 * s; x <= 4 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy + s, color);
            }

            // Turrets (top) - scaled
            for (int i = 0; i <= s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx - 2 * s, cy - s + i, color);
                DrawPixel(pBackBuffer, stride, cx + 2 * s, cy - s + i, color);
            }
        }

        private unsafe void DrawPatrolBoatIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Small hull - scaled
            for (int x = -2 * s; x <= 2 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy + s, color);
            }

            // Cabin - scaled
            for (int y = -s; y <= 0; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);
        }

        private unsafe void DrawTransportIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Hull - scaled
            for (int x = -4 * s; x <= 3 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy + s, color);
            }

            // Cargo containers (boxes on deck) - scaled
            for (int i = 0; i <= s; i++)
            {
                DrawPixel(pBackBuffer, stride, cx - 2 * s, cy - s + i, color);
                DrawPixel(pBackBuffer, stride, cx + s, cy - s + i, color);
            }
        }

        private unsafe void DrawSpyIcon(IntPtr pBackBuffer, int stride, int startX, int startY, bool isVeteran)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = isVeteran ? Colors.Yellow : Colors.White;

            // Head with hat brim - scaled
            for (int x = -2 * s; x <= 2 * s; x++)
                DrawPixel(pBackBuffer, stride, cx + x, cy - 3 * s, color);
            DrawPixel(pBackBuffer, stride, cx, cy - 2 * s, color);

            // Body (coat) - scaled
            for (int y = -s; y <= 2 * s; y++)
            {
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);
                if (y >= 0)
                {
                    DrawPixel(pBackBuffer, stride, cx - s, cy + y, color);
                    DrawPixel(pBackBuffer, stride, cx + s, cy + y, color);
                }
            }
        }

        private unsafe void DrawBaseIcon(IntPtr pBackBuffer, int stride, int startX, int startY)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = Colors.White;

            // Pole - scaled
            for (int y = -4 * s; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);

            // Flag - scaled
            for (int x = s; x <= 3 * s; x++)
            {
                DrawPixel(pBackBuffer, stride, cx + x, cy - 3 * s, color);
                DrawPixel(pBackBuffer, stride, cx + x, cy - 2 * s, color);
            }
        }

        private unsafe void DrawCityIcon(IntPtr pBackBuffer, int stride, int startX, int startY)
        {
            int cx = startX + tileSize / 2;
            int cy = startY + tileSize / 2;
            int s = Math.Max(1, (int)iconScale);

            Color color = Colors.White;

            // Three buildings of different heights - scaled
            // Left building
            for (int y = 0; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx - 3 * s, cy + y, color);

            // Middle building (tallest)
            for (int y = -2 * s; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx, cy + y, color);

            // Right building
            for (int y = s; y <= 3 * s; y++)
                DrawPixel(pBackBuffer, stride, cx + 3 * s, cy + y, color);
        }

        private unsafe void DrawPixel(IntPtr pBackBuffer, int stride, int x, int y, Color color)
        {
            if (x >= 0 && x < bitmap.PixelWidth && y >= 0 && y < bitmap.PixelHeight)
            {
                byte* pixel = (byte*)pBackBuffer + y * stride + x * 4;
                pixel[0] = color.B;
                pixel[1] = color.G;
                pixel[2] = color.R;
                pixel[3] = 255;
            }
        }

        // Keep all the existing helper methods (DrawCircle, DrawTriangle, DrawDiamond, etc.)

        private unsafe void DrawCircle(IntPtr pBackBuffer, int stride, int startX, int startY, Color color)
        {
            int centerX = startX + tileSize / 2;
            int centerY = startY + tileSize / 2;
            int radius = (int)((tileSize / 2.0 - 2) * 0.8); // Scale the radius

            for (int py = -radius; py <= radius; py++)
            {
                for (int px = -radius; px <= radius; px++)
                {
                    if (px * px + py * py <= radius * radius)
                    {
                        int screenX = centerX + px;
                        int screenY = centerY + py;

                        if (screenX >= startX && screenX < startX + tileSize &&
                            screenY >= startY && screenY < startY + tileSize)
                        {
                            byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                            pixel[0] = color.B;
                            pixel[1] = color.G;
                            pixel[2] = color.R;
                            pixel[3] = 255;
                        }
                    }
                }
            }
        }

        private unsafe void DrawTriangle(IntPtr pBackBuffer, int stride, int startX, int startY, Color color)
        {
            int centerX = startX + tileSize / 2;
            int margin = (int)(2 * iconScale);
            int topY = startY + margin;
            int bottomY = startY + tileSize - margin;
            int leftX = startX + margin;
            int rightX = startX + tileSize - margin;

            for (int y = topY; y <= bottomY; y++)
            {
                float ratio = (float)(y - topY) / (bottomY - topY);
                int halfWidth = (int)((rightX - leftX) / 2 * ratio);

                for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
                {
                    if (x >= startX && x < startX + tileSize &&
                        y >= startY && y < startY + tileSize)
                    {
                        byte* pixel = (byte*)pBackBuffer + y * stride + x * 4;
                        pixel[0] = color.B;
                        pixel[1] = color.G;
                        pixel[2] = color.R;
                        pixel[3] = 255;
                    }
                }
            }
        }

        private unsafe void DrawDiamond(IntPtr pBackBuffer, int stride, int startX, int startY, Color color)
        {
            int centerX = startX + tileSize / 2;
            int centerY = startY + tileSize / 2;
            int halfSize = (int)((tileSize / 2.0 - 2) * 0.8);

            for (int py = -halfSize; py <= halfSize; py++)
            {
                int width = halfSize - Math.Abs(py);
                for (int px = -width; px <= width; px++)
                {
                    int screenX = centerX + px;
                    int screenY = centerY + py;

                    if (screenX >= startX && screenX < startX + tileSize &&
                        screenY >= startY && screenY < startY + tileSize)
                    {
                        byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                        pixel[0] = color.B;
                        pixel[1] = color.G;
                        pixel[2] = color.R;
                        pixel[3] = 255;
                    }
                }
            }
        }

        private unsafe void DrawHighlight(IntPtr pBackBuffer, int stride, int x, int y, Color color)
        {
            int thickness = 2;

            for (int t = 0; t < thickness; t++)
            {
                for (int px = 0; px < tileSize; px++)
                {
                    byte* pixelTop = (byte*)pBackBuffer + (y + t) * stride + (x + px) * 4;
                    pixelTop[0] = color.B;
                    pixelTop[1] = color.G;
                    pixelTop[2] = color.R;
                    pixelTop[3] = 255;

                    byte* pixelBottom = (byte*)pBackBuffer + (y + tileSize - 1 - t) * stride + (x + px) * 4;
                    pixelBottom[0] = color.B;
                    pixelBottom[1] = color.G;
                    pixelBottom[2] = color.R;
                    pixelBottom[3] = 255;
                }

                for (int py = 0; py < tileSize; py++)
                {
                    byte* pixelLeft = (byte*)pBackBuffer + (y + py) * stride + (x + t) * 4;
                    pixelLeft[0] = color.B;
                    pixelLeft[1] = color.G;
                    pixelLeft[2] = color.R;
                    pixelLeft[3] = 255;

                    byte* pixelRight = (byte*)pBackBuffer + (y + py) * stride + (x + tileSize - 1 - t) * 4;
                    pixelRight[0] = color.B;
                    pixelRight[1] = color.G;
                    pixelRight[2] = color.R;
                    pixelRight[3] = 255;
                }
            }
        }

        private unsafe void DrawBorder(IntPtr pBackBuffer, int stride, int x, int y, int width, int height, Color color)
        {
            for (int px = 0; px < width; px++)
            {
                byte* pixelTop = (byte*)pBackBuffer + y * stride + (x + px) * 4;
                pixelTop[0] = color.B;
                pixelTop[1] = color.G;
                pixelTop[2] = color.R;
                pixelTop[3] = 255;

                byte* pixelBottom = (byte*)pBackBuffer + (y + height - 1) * stride + (x + px) * 4;
                pixelBottom[0] = color.B;
                pixelBottom[1] = color.G;
                pixelBottom[2] = color.R;
                pixelBottom[3] = 255;
            }

            for (int py = 0; py < height; py++)
            {
                byte* pixelLeft = (byte*)pBackBuffer + (y + py) * stride + x * 4;
                pixelLeft[0] = color.B;
                pixelLeft[1] = color.G;
                pixelLeft[2] = color.R;
                pixelLeft[3] = 255;

                byte* pixelRight = (byte*)pBackBuffer + (y + py) * stride + (x + width - 1) * 4;
                pixelRight[0] = color.B;
                pixelRight[1] = color.G;
                pixelRight[2] = color.R;
                pixelRight[3] = 255;
            }
        }

        private Color GetTerrainColor(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Ocean => OceanColor,
                TerrainType.CoastalWater => CoastalWaterColor,
                TerrainType.Land => LandColor,
                TerrainType.Plains => PlainsColor,
                TerrainType.Forest => ForestColor,
                TerrainType.Hills => HillsColor,
                TerrainType.Mountain => MountainColor,
                _ => LandColor
            };
        }

        private Color GetPlayerColor(int playerId)
        {
            if (playerId < 0 || playerId >= PlayerColors.Length)
                return Colors.Gray;

            return PlayerColors[playerId];
        }
    }
}