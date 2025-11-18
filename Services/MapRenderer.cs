using System;
using System.Collections.Generic;
using System.IO;
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
        private string iconBasePath;

        // Resource icon bitmaps
        private BitmapSource oilIcon;
        private BitmapSource steelIcon;

        // Unit sprite dictionaries (not player-specific)
        private Dictionary<string, BitmapSource> unitSprites = new Dictionary<string, BitmapSource>();
        private Dictionary<string, BitmapSource> veteranSprites = new Dictionary<string, BitmapSource>();

        // Structure sprites
        private Dictionary<string, BitmapSource> structureSprites = new Dictionary<string, BitmapSource>();

        // Satellite sprites
        private BitmapSource orbitingSatelliteSprite;
        private BitmapSource geosynchronousSatelliteSprite;

        // Mapping from class names to icon file names
        private static readonly Dictionary<string, string> IconFileNameMap = new Dictionary<string, string>
        {
            { "Army", "Army" },
            { "Tank", "Tank" },
            { "Artillery", "Artillery" },
            { "AntiAircraft", "AntiAircraft" },
            { "Spy", "Spy" },
            { "Fighter", "Fighter" },
            { "Bomber", "Bomber" },
            { "Tanker", "Fighter" },  // Use Fighter icon as fallback for Tanker
            { "Carrier", "AircraftCarrier" },
            { "Battleship", "Battleship" },
            { "Destroyer", "Destroyer" },
            { "Submarine", "Submarine" },
            { "PatrolBoat", "PatrolBoat" },
            { "Transport", "PatrolBoat" },  // Use PatrolBoat icon as fallback for Transport
            { "City", "City" },
            { "Base", "Base" },
            { "Sapper", "Sapper" }
        };

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

            // Set base path for icons (in application directory)
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            iconBasePath = Path.Combine(appDirectory, "Resources\\Empire_Icons");

            int width = game.Map.Width * tileSize;
            int height = game.Map.Height * tileSize;

            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);

            LoadResourceIcons();
            LoadSprites();
        }

        private void LoadResourceIcons()
        {
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

        private void LoadSprites()
        {
            System.Diagnostics.Debug.WriteLine($"Looking for icons at: {iconBasePath}");
            System.Diagnostics.Debug.WriteLine($"Directory exists: {Directory.Exists(iconBasePath)}");

            // Load regular unit sprites from Units folder
            string unitsPath = Path.Combine(iconBasePath, "Units");
            if (Directory.Exists(unitsPath))
            {
                System.Diagnostics.Debug.WriteLine($"Units folder exists: {unitsPath}");

                foreach (var kvp in IconFileNameMap)
                {
                    string unitType = kvp.Key;
                    string fileName = kvp.Value;

                    string unitPath = Path.Combine(unitsPath, $"{fileName}.png");

                    if (File.Exists(unitPath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(unitPath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            unitSprites[unitType] = bitmap;
                            System.Diagnostics.Debug.WriteLine($"✓ Loaded: Units/{fileName}.png ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to load {unitPath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ File not found: {unitPath}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"✗ Units folder not found: {unitsPath}");
            }

            // Load veteran unit sprites from Veteran folder
            string veteranPath = Path.Combine(iconBasePath, "Veteran");
            if (Directory.Exists(veteranPath))
            {
                System.Diagnostics.Debug.WriteLine($"Veteran folder exists: {veteranPath}");

                foreach (var kvp in IconFileNameMap)
                {
                    string unitType = kvp.Key;
                    string fileName = kvp.Value;

                    string vetPath = Path.Combine(veteranPath, $"{fileName}.png");

                    if (File.Exists(vetPath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(vetPath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            veteranSprites[unitType] = bitmap;
                            System.Diagnostics.Debug.WriteLine($"✓ Loaded: Veteran/{fileName}.png ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to load {vetPath}: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"✗ Veteran folder not found: {veteranPath}");
            }

            // Load structure sprites from Units folder
            string[] structureTypes = new string[] { "City", "Base" };
            foreach (string structureType in structureTypes)
            {
                string structPath = Path.Combine(unitsPath, $"{structureType}.png");

                if (File.Exists(structPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(structPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        structureSprites[structureType] = bitmap;
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded: Units/{structureType}.png ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to load {structPath}: {ex.Message}");
                    }
                }
            }

            // Load satellite sprites
            string orbitingSatPath = Path.Combine(unitsPath, "OrbitingSatellite.png");
            if (File.Exists(orbitingSatPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(orbitingSatPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    orbitingSatelliteSprite = bitmap;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded: OrbitingSatellite.png ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to load {orbitingSatPath}: {ex.Message}");
                }
            }

            string geoSatPath = Path.Combine(unitsPath, "GeosynchronousSatellite.png");
            if (File.Exists(geoSatPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(geoSatPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    geosynchronousSatelliteSprite = bitmap;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded: GeosynchronousSatellite.png ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to load {geoSatPath}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Sprite loading complete. Icons will be scaled to fit tiles ({tileSize}x{tileSize})");
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

            if (tile.HasBridge)
            {
                RenderBridge(pBackBuffer, stride, tileX, tileY);
            }
        }

        private unsafe void RenderResourceIcon(IntPtr pBackBuffer, int stride, int tileX, int tileY, ResourceType resource)
        {
            BitmapSource icon = resource == ResourceType.Oil ? oilIcon : steelIcon;

            if (icon == null)
                return;

            // Calculate position (top-right corner of tile)
            int iconDisplaySize = Math.Min(16, tileSize - 4);
            int destX = tileX * tileSize + tileSize - iconDisplaySize - 2;
            int destY = tileY * tileSize + 2;

            RenderSprite(pBackBuffer, stride, icon, destX, destY, iconDisplaySize, iconDisplaySize);
        }

        private unsafe void RenderStructure(IntPtr pBackBuffer, int stride, int tileX, int tileY, Structure structure)
        {
            string structureType = structure is City ? "City" : "Base";
            Color playerColor = GetPlayerColor(structure.OwnerId);

            BitmapSource sprite = null;
            if (structureSprites.ContainsKey(structureType))
            {
                sprite = structureSprites[structureType];
            }

            if (sprite != null)
            {
                // Draw colored circle background
                DrawColorRing(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, playerColor);

                // Render sprite
                int spriteSize = tileSize - 8;
                int offsetX = 4;
                int offsetY = 4;
                int destX = tileX * tileSize + offsetX;
                int destY = tileY * tileSize + offsetY;

                RenderSprite(pBackBuffer, stride, sprite, destX, destY, spriteSize, spriteSize);

                // Show damage indicator if structure is damaged
                double lifePercent = structure.Life / (double)structure.MaxLife;
                if (lifePercent < 1.0)
                {
                    // Draw damage bar at bottom of tile
                    int barHeight = 4;
                    int barWidth = tileSize - 8;
                    int barX = tileX * tileSize + 4;
                    int barY = tileY * tileSize + tileSize - barHeight - 2;

                    // Background (red)
                    for (int py = 0; py < barHeight; py++)
                    {
                        for (int px = 0; px < barWidth; px++)
                        {
                            int screenX = barX + px;
                            int screenY = barY + py;

                            byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                            pixel[0] = 0;   // B
                            pixel[1] = 0;   // G
                            pixel[2] = 128; // R
                            pixel[3] = 255; // A
                        }
                    }

                    // Foreground (green for life remaining)
                    int lifeBarWidth = (int)(barWidth * lifePercent);
                    Color lifeColor = lifePercent > 0.5 ? Color.FromRgb(0, 255, 0) : Color.FromRgb(255, 255, 0);

                    for (int py = 0; py < barHeight; py++)
                    {
                        for (int px = 0; px < lifeBarWidth; px++)
                        {
                            int screenX = barX + px;
                            int screenY = barY + py;

                            byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                            pixel[0] = lifeColor.B;
                            pixel[1] = lifeColor.G;
                            pixel[2] = lifeColor.R;
                            pixel[3] = 255;
                        }
                    }
                }
            }
            else
            {
                // Fallback rendering
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
                        pixel[0] = playerColor.B;
                        pixel[1] = playerColor.G;
                        pixel[2] = playerColor.R;
                        pixel[3] = 255;
                    }
                }
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

            // Check if this is a satellite (satellites don't have player-specific colors)
            string unitTypeName = unit.GetType().Name;
            if (unitTypeName == "OrbitingSatellite" || unitTypeName == "GeosynchronousSatellite")
            {
                BitmapSource satelliteSprite = unitTypeName == "OrbitingSatellite" ? orbitingSatelliteSprite : geosynchronousSatelliteSprite;

                if (satelliteSprite != null)
                {
                    int spriteSize = tileSize - 4;
                    int offsetX = 2;
                    int offsetY = 2;
                    int destX = tileX * tileSize + offsetX;
                    int destY = tileY * tileSize + offsetY;

                    RenderSprite(pBackBuffer, stride, satelliteSprite, destX, destY, spriteSize, spriteSize);
                    return;
                }
            }

            string unitType = GetUnitTypeName(unit, currentPlayer);
            Color playerColor = GetPlayerColor(unit.OwnerId);
            bool isVeteran = unit.IsVeteran;

            // Select the appropriate sprite based on veteran status
            BitmapSource sprite = null;
            if (isVeteran && veteranSprites.ContainsKey(unitType))
            {
                sprite = veteranSprites[unitType];
            }
            else if (unitSprites.ContainsKey(unitType))
            {
                sprite = unitSprites[unitType];
            }

            if (sprite != null)
            {
                // Draw colored circle background to indicate owner
                DrawColorRing(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, playerColor);

                // Render sprite centered on tile, scaled to fit (slightly smaller to show colored ring)
                int spriteSize = tileSize - 8; // Leave more margin to show colored background
                int offsetX = 4;
                int offsetY = 4;
                int destX = tileX * tileSize + offsetX;
                int destY = tileY * tileSize + offsetY;

                RenderSprite(pBackBuffer, stride, sprite, destX, destY, spriteSize, spriteSize);
            }
            else
            {
                // Simple fallback - draw basic shape based on unit type
                if (unit is AirUnit)
                {
                    DrawTriangle(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, playerColor);
                }
                else if (unit is SeaUnit)
                {
                    DrawDiamond(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, playerColor);
                }
                else
                {
                    DrawCircle(pBackBuffer, stride, tileX * tileSize, tileY * tileSize, playerColor);
                }
            }
        }

        private string GetUnitTypeName(Unit unit, Player currentPlayer)
        {
            // Check if spy is disguised
            if (unit is Spy spy && !spy.IsRevealed && unit.OwnerId != currentPlayer.PlayerId)
            {
                return "Army";
            }

            return unit.GetType().Name;
        }

        private unsafe void RenderSprite(IntPtr pBackBuffer, int stride, BitmapSource sprite, int destX, int destY, int renderWidth, int renderHeight)
        {
            if (sprite == null)
                return;

            // Convert sprite to Bgra32 format if needed
            BitmapSource convertedSprite = sprite;
            if (sprite.Format != PixelFormats.Bgra32)
            {
                convertedSprite = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
            }

            int spriteWidth = convertedSprite.PixelWidth;
            int spriteHeight = convertedSprite.PixelHeight;

            // Get sprite pixel data
            int spriteStride = spriteWidth * 4;
            byte[] spritePixels = new byte[spriteStride * spriteHeight];
            convertedSprite.CopyPixels(spritePixels, spriteStride, 0);

            // Calculate scale factors for resizing
            float scaleX = (float)spriteWidth / renderWidth;
            float scaleY = (float)spriteHeight / renderHeight;

            // Copy sprite pixels to the main bitmap with scaling and alpha blending
            for (int y = 0; y < renderHeight; y++)
            {
                for (int x = 0; x < renderWidth; x++)
                {
                    int screenX = destX + x;
                    int screenY = destY + y;

                    // Check bounds
                    if (screenX < 0 || screenX >= bitmap.PixelWidth ||
                        screenY < 0 || screenY >= bitmap.PixelHeight)
                        continue;

                    // Sample from sprite with scaling (nearest neighbor)
                    int srcX = (int)(x * scaleX);
                    int srcY = (int)(y * scaleY);

                    // Clamp to sprite bounds
                    srcX = Math.Min(srcX, spriteWidth - 1);
                    srcY = Math.Min(srcY, spriteHeight - 1);

                    // Get sprite pixel
                    int spriteOffset = srcY * spriteStride + srcX * 4;
                    byte spriteB = spritePixels[spriteOffset];
                    byte spriteG = spritePixels[spriteOffset + 1];
                    byte spriteR = spritePixels[spriteOffset + 2];
                    byte spriteA = spritePixels[spriteOffset + 3];

                    // Skip fully transparent pixels
                    if (spriteA == 0)
                        continue;

                    // Write to destination with alpha blending
                    byte* destPixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;

                    if (spriteA == 255)
                    {
                        // Fully opaque - just copy
                        destPixel[0] = spriteB;
                        destPixel[1] = spriteG;
                        destPixel[2] = spriteR;
                        destPixel[3] = 255;
                    }
                    else
                    {
                        // Alpha blend
                        float alpha = spriteA / 255.0f;
                        destPixel[0] = (byte)(spriteB * alpha + destPixel[0] * (1 - alpha));
                        destPixel[1] = (byte)(spriteG * alpha + destPixel[1] * (1 - alpha));
                        destPixel[2] = (byte)(spriteR * alpha + destPixel[2] * (1 - alpha));
                        destPixel[3] = 255;
                    }
                }
            }
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

        private unsafe void DrawColorRing(IntPtr pBackBuffer, int stride, int startX, int startY, Color color)
        {
            int centerX = startX + tileSize / 2;
            int centerY = startY + tileSize / 2;
            int outerRadius = (int)((tileSize / 2.0 - 1) * 0.95);
            int innerRadius = outerRadius - 3; // Ring thickness of 3 pixels

            for (int py = -outerRadius; py <= outerRadius; py++)
            {
                for (int px = -outerRadius; px <= outerRadius; px++)
                {
                    int distanceSquared = px * px + py * py;

                    // Check if pixel is within the ring (between inner and outer radius)
                    if (distanceSquared <= outerRadius * outerRadius &&
                        distanceSquared >= innerRadius * innerRadius)
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

        private unsafe void DrawCircle(IntPtr pBackBuffer, int stride, int startX, int startY, Color color)
        {
            int centerX = startX + tileSize / 2;
            int centerY = startY + tileSize / 2;
            int radius = (int)((tileSize / 2.0 - 2) * 0.8);

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

        private unsafe void RenderBridge(IntPtr pBackBuffer, int stride, int tileX, int tileY)
        {
            // Draw a brown bridge across the tile
            Color bridgeColor = Color.FromRgb(139, 90, 43);
            int startX = tileX * tileSize;
            int startY = tileY * tileSize;

            // Draw horizontal planks
            int plankHeight = 3;
            int spacing = tileSize / 4;

            for (int p = 0; p < 4; p++)
            {
                int plankY = startY + (p * spacing);
                for (int py = 0; py < plankHeight; py++)
                {
                    for (int px = 0; px < tileSize; px++)
                    {
                        int screenX = startX + px;
                        int screenY = plankY + py;

                        if (screenY >= startY && screenY < startY + tileSize)
                        {
                            byte* pixel = (byte*)pBackBuffer + screenY * stride + screenX * 4;
                            pixel[0] = bridgeColor.B;
                            pixel[1] = bridgeColor.G;
                            pixel[2] = bridgeColor.R;
                            pixel[3] = 255;
                        }
                    }
                }
            }
        }
    }
}
