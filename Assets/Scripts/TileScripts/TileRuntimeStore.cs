using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

public class TileRuntimeStore : MonoBehaviour
{
    public class Runtime
    {
        public bool occupied;
        public bool available;
        public GameObject occupantInstance;
        public GameObject availabilityInstance;
        public GameObject templatePrefab;
        public TileDraw tileDraw;
        public TileType tileType = TileType.Field;
    }

    private readonly Dictionary<Tile, Runtime> _map = new();
    private int _occupiedCount;

    public Runtime Get(Tile t)
    {
        if (t == null) return null;
        if (!_map.TryGetValue(t, out var r)) _map[t] = r = new Runtime();
        return r;
    }
    public int OccupiedCount => _occupiedCount;
    public void MarkOccupied(Tile t, GameObject inst, GameObject template = null, TileDraw tileDraw = null)
    {
        var r = Get(t);
        if (!r.occupied) _occupiedCount++;
        r.occupied = true;
        r.available = false;
        r.occupantInstance = inst;
        if (template) r.templatePrefab = template;
        if (tileDraw != null)
        {
            r.tileDraw = tileDraw;
            r.tileType = tileDraw.tileType;
            if (!template && tileDraw.prefab)
                r.templatePrefab = tileDraw.prefab;
        }
    }

    public void Free(Tile t)
    {
        var r = Get(t);
        if (r.occupied && _occupiedCount > 0) _occupiedCount--;
        r.occupied = false;
        r.available = true;
        r.tileDraw = null;
        r.occupantInstance = null;
    }

}