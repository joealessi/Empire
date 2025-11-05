public class Map
{
    public int Width { get; set; }
    public int Height { get; set; }
    private Tile[,] tiles;
    
    public Map(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new Tile[width, height];
        
        InitializeTiles();
    }
    
    private void InitializeTiles()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = new Tile(
                    new TilePosition(x, y), 
                    TerrainType.Ocean); // Default to ocean, will be set during map generation
            }
        }
    }
    
    public bool IsValidPosition(TilePosition pos)
    {
        return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
    }
    
    public Tile GetTile(TilePosition pos)
    {
        if (!IsValidPosition(pos))
            return null;
        
        return tiles[pos.X, pos.Y];
    }
    
    public void SetTile(TilePosition pos, Tile tile)
    {
        if (IsValidPosition(pos))
        {
            tiles[pos.X, pos.Y] = tile;
        }
    }
    
    public List<Tile> GetNeighbors(TilePosition pos)
    {
        var neighbors = new List<Tile>();
    
        // Include all 8 directions (orthogonal + diagonal)
        int[] dx = { -1, 0, 1, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 1, 0, -1, -1, -1, 1, 1 };
    
        for (int i = 0; i < 8; i++)
        {
            var neighborPos = new TilePosition(pos.X + dx[i], pos.Y + dy[i]);
            if (IsValidPosition(neighborPos))
            {
                neighbors.Add(GetTile(neighborPos));
            }
        }
    
        return neighbors;
    }
    
    public List<Tile> GetTilesInRadius(TilePosition center, int radius)
    {
        var tiles = new List<Tile>();
        
        for (int x = center.X - radius; x <= center.X + radius; x++)
        {
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var pos = new TilePosition(x, y);
                if (IsValidPosition(pos))
                {
                    int distance = Math.Abs(x - center.X) + Math.Abs(y - center.Y);
                    if (distance <= radius)
                    {
                        tiles.Add(GetTile(pos));
                    }
                }
            }
        }
        
        return tiles;
    }
    
    public bool HasLineOfSight(TilePosition from, TilePosition to)
    {
        // Bresenham's line algorithm to check if mountains block line of sight
        int x0 = from.X;
        int y0 = from.Y;
        int x1 = to.X;
        int y1 = to.Y;
        
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        while (true)
        {
            var currentPos = new TilePosition(x0, y0);
            var tile = GetTile(currentPos);
            
            // Mountains block line of sight
            if (tile != null && tile.Terrain == TerrainType.Mountain && 
                (x0 != from.X || y0 != from.Y) && 
                (x0 != to.X || y0 != to.Y))
            {
                return false;
            }
            
            if (x0 == x1 && y0 == y1)
                break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        
        return true;
    }
    
    public List<TilePosition> FindPath(TilePosition start, TilePosition goal, Unit unit)
    {
        // A* pathfinding - diagonal movement costs same as horizontal (terrain-based only)
        var openSet = new SortedSet<PathNode>(new PathNodeComparer());
        var closedSet = new HashSet<TilePosition>();
        var cameFrom = new Dictionary<TilePosition, TilePosition>();
        var gScore = new Dictionary<TilePosition, double>();
        var fScore = new Dictionary<TilePosition, double>();
        
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Add(new PathNode(start, (int)fScore[start]));
        
        while (openSet.Count > 0)
        {
            var current = openSet.Min;
            openSet.Remove(current);
            
            if (current.Position.Equals(goal))
            {
                return ReconstructPath(cameFrom, current.Position);
            }
            
            closedSet.Add(current.Position);
            
            foreach (var neighbor in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighbor.Position))
                    continue;
                
                if (!neighbor.CanUnitEnter(unit))
                    continue;
                
                // Movement cost is based on terrain only - no diagonal penalty
                double moveCost = neighbor.GetMovementCost(unit);
                
                double tentativeGScore = gScore[current.Position] + moveCost;
                
                if (!gScore.ContainsKey(neighbor.Position) || tentativeGScore < gScore[neighbor.Position])
                {
                    cameFrom[neighbor.Position] = current.Position;
                    gScore[neighbor.Position] = tentativeGScore;
                    fScore[neighbor.Position] = tentativeGScore + Heuristic(neighbor.Position, goal);
                    
                    openSet.Add(new PathNode(neighbor.Position, (int)fScore[neighbor.Position]));
                }
            }
        }
        
        return new List<TilePosition>(); // No path found
    }
        
    private bool IsDiagonal(TilePosition from, TilePosition to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        return dx == 1 && dy == 1;
    }

    private int Heuristic(TilePosition a, TilePosition b)
    {
        // Manhattan distance - diagonal costs same as straight
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return dx + dy;
    }

    private List<TilePosition> ReconstructPath(Dictionary<TilePosition, TilePosition> cameFrom, TilePosition current)
    {
        var path = new List<TilePosition> { current };
        
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        
        return path;
    }
}