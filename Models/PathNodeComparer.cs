public class PathNodeComparer : IComparer<PathNode>
{
    public int Compare(PathNode x, PathNode y)
    {
        int result = x.FScore.CompareTo(y.FScore);
        if (result == 0)
        {
            result = x.Position.X.CompareTo(y.Position.X);
            if (result == 0)
            {
                result = x.Position.Y.CompareTo(y.Position.Y);
            }
        }
        return result;
    }
}