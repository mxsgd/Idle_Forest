using UnityEngine;
using Tile = TileGrid.Tile;

public class TileQueryService : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtime;

    public bool TryGetNearestTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        float bestSqr = maxDistance * maxDistance;
        foreach (var t in grid.tiles)
        {
            float d = (worldPoint - t.worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; tile = t; }
        }
        return tile != null;
    }

    public bool TryGetNearestFreeTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        float bestSqr = maxDistance * maxDistance;
        foreach (var t in grid.tiles)
        {
            if (runtime.Get(t).occupied) continue;
            float d = (worldPoint - t.worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; tile = t; }
        }
        return tile != null;
    }
}