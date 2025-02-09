using UnityEngine;

public struct QuadrantProcessInfo
{
    public Vector2Int Position;
    public bool ShouldLoad;  // true = load, false = unload

    public QuadrantProcessInfo(Vector2Int pos, bool load)
    {
        Position = pos;
        ShouldLoad = load;
    }
}