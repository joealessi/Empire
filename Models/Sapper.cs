public class Sapper : LandUnit
{
    public int BuildProgress { get; set; }
    public bool IsBuildingBase { get; set; }
    public bool IsBuildingBridge { get; set; }
    public TilePosition BuildTarget { get; set; }

    public Sapper()
    {
        MaxLife = 10;
        Life = MaxLife;
        Power = 0;  // No attack capability
        MaxPower = 0;
        Toughness = 1;  // Minimal defense
        MaxToughness = 1;
        MaxMovementPoints = 2;
        MovementPoints = MaxMovementPoints;
        BuildProgress = 0;
        IsBuildingBase = false;
        IsBuildingBridge = false;
        BuildTarget = new TilePosition(-1, -1);
    }

    public string GetName()
    {
        return "Sapper";
    }

    public override bool CanMoveOn(TerrainType terrain)
    {
        // Sappers are land units - can't move on water
        return terrain != TerrainType.Ocean && terrain != TerrainType.CoastalWater;
    }

    public void StartBuildingBase(TilePosition position)
    {
        IsBuildingBase = true;
        IsBuildingBridge = false;
        BuildTarget = position;
        BuildProgress = 0;
    }

    public void StartBuildingBridge(TilePosition position)
    {
        IsBuildingBridge = true;
        IsBuildingBase = false;
        BuildTarget = position;
        BuildProgress = 0;
    }

    public void ProgressBuild()
    {
        if (IsBuildingBase || IsBuildingBridge)
        {
            BuildProgress++;
        }
    }

    public bool IsBuildComplete()
    {
        if (IsBuildingBase && BuildProgress >= 2)
            return true;
        if (IsBuildingBridge && BuildProgress >= 1)
            return true;
        return false;
    }

    public void ResetBuild()
    {
        BuildProgress = 0;
        IsBuildingBase = false;
        IsBuildingBridge = false;
        BuildTarget = new TilePosition(-1, -1);
    }

    public bool CanBuildBaseAt(TilePosition position, Map map)
    {
        if (!map.IsValidPosition(position))
            return false;

        var tile = map.GetTile(position);
        
        // Can only build on land terrain (not water)
        if (tile.Terrain == TerrainType.Ocean || tile.Terrain == TerrainType.CoastalWater)
            return false;

        // Can't build if there's already a structure
        if (tile.Structure != null)
            return false;

        // Must be on or adjacent to the target position
        int distance = Math.Abs(Position.X - position.X) + Math.Abs(Position.Y - position.Y);
        return distance <= 1;
    }

    public bool CanBuildBridgeAt(TilePosition position, Map map)
    {
        if (!map.IsValidPosition(position))
            return false;

        var tile = map.GetTile(position);
        
        // Can only build bridge on single-tile water
        if (tile.Terrain != TerrainType.Ocean && tile.Terrain != TerrainType.CoastalWater)
            return false;

        // Check if this is a single tile water feature (has land on at least 2 sides)
        int landNeighbors = 0;
        int[] dx = { -1, 0, 1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        for (int i = 0; i < 4; i++)
        {
            var neighborPos = new TilePosition(position.X + dx[i], position.Y + dy[i]);
            if (map.IsValidPosition(neighborPos))
            {
                var neighbor = map.GetTile(neighborPos);
                if (neighbor.Terrain != TerrainType.Ocean && neighbor.Terrain != TerrainType.CoastalWater)
                {
                    landNeighbors++;
                }
            }
        }

        // Must have at least 2 land neighbors to be bridgeable
        if (landNeighbors < 2)
            return false;

        // Can't build if there's already a bridge
        if (tile.HasBridge)
            return false;

        // Must be on or adjacent to the target position
        int distance = Math.Abs(Position.X - position.X) + Math.Abs(Position.Y - position.Y);
        return distance <= 1;
    }
}