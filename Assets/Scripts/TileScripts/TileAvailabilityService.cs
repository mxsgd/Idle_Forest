using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

public class TileAvailabilityService : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtime;

    public IEnumerable<Tile> GetAvailable()
    {
        if (grid.tiles == null || grid.tiles.Count == 0) yield break;

        foreach (var t in grid.tiles) runtime.Get(t).available = false;

        foreach (var t in grid.tiles)
        {
            var rt = runtime.Get(t);
            if (!rt.occupied) continue;

            foreach (var n in grid.GetNeighbors(t))
            {
                var rn = runtime.Get(n);
                if (rn.occupied) continue;
                rn.available = true;

                if (rn.templatePrefab == null) rn.templatePrefab = rt.templatePrefab;
            }
        }

        foreach (var t in grid.tiles)
        {
            var r = runtime.Get(t);
            if (r.available && !r.occupied) yield return t;
        }
    }
}