using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Collider))]

public class TileGrid : MonoBehaviour
{
    [Header("Grid")]
    [Min(1)] public int rows = 200;
    [Min(1)] public int cols = 200;
    public bool rebuildOnValidate = true;

    [System.Serializable]
    public class Tile
    {
        public int i, j;
        public int q, r;
        public Vector3 worldPos;
    }

    public List<Tile> tiles = new List<Tile>();
    private Tile[,] _grid;
    private readonly Dictionary<Vector2Int, Tile> _axialLookup = new Dictionary<Vector2Int, Tile>();
    private Tile _centerTile;
    private static readonly Vector2Int[] AxialDirections =
    {
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1)
    };

    private float _hexScale = 1f;
    private Bounds _localBounds;

    void OnEnable() { CacheBounds(); BuildGrid(); }

    void OnValidate()
    {
        if (!Application.isPlaying && rebuildOnValidate) { CacheBounds(); BuildGrid(); }
    }

    void CacheBounds()
    {
        var mf = GetComponent<MeshFilter>();
        _localBounds = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    public void BuildGrid()
    {
        tiles.Clear();
        _axialLookup.Clear();
        _centerTile = null;

        if (rows < 1 || cols < 1) return;
        _grid = new Tile[rows, cols];

        var localPositions = new Vector2[rows * cols];
        int index = 0;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                float q = j - (cols - 1) * 0.5f;
                float r = i - (rows - 1) * 0.5f;
                float x = Mathf.Sqrt(3f) * (q + r * 0.5f);
                float z = 1.5f * r;
                localPositions[index++] = new Vector2(x, z);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }

        var sizeLocal = _localBounds.size;
        float width = Mathf.Max(0.0001f, maxX - minX);
        float depth = Mathf.Max(0.0001f, maxZ - minZ);
        _hexScale = Mathf.Min(sizeLocal.x / width, sizeLocal.z / depth);

        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        index = 0;
        Vector3 boundsCenter = _localBounds.center;
        Vector3 gridWorldCenter = transform.TransformPoint(boundsCenter);
        float bestDistance = float.PositiveInfinity;

        int q0 = Mathf.RoundToInt((cols - 1) * 0.5f);
        int r0 = Mathf.RoundToInt((rows - 1) * 0.5f);

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                Vector2 coords = localPositions[index++];
                float scaledX = (coords.x - centerX) * _hexScale;
                float scaledZ = (coords.y - centerZ) * _hexScale;
                Vector3 localCenter = new Vector3(boundsCenter.x + scaledX, boundsCenter.y, boundsCenter.z + scaledZ);
                Vector3 worldCenter = transform.TransformPoint(localCenter);

                Vector3 rayStart = worldCenter + transform.up * 5f;
                if (Physics.Raycast(rayStart, -transform.up, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    worldCenter = hit.point;

                int axialQ = j - q0;
                int axialR = i - r0;

                var t = new Tile
                {
                    i = i,
                    j = j,
                    q = axialQ,
                    r = axialR,
                    worldPos = worldCenter,
                };

                tiles.Add(t);
                _grid[i, j] = t;
                _axialLookup[new Vector2Int(t.q, t.r)] = t;

                float sqr = (worldCenter - gridWorldCenter).sqrMagnitude;
                if (sqr < bestDistance)
                {
                    bestDistance = sqr;
                    _centerTile = t;
                }
            }
    }
    public Tile GetTile(int i, int j) => (i >= 0 && i < rows && j >= 0 && j < cols) ? _grid[i, j] : null;

    public IEnumerable<Tile> GetNeighbors(Tile tile)
    {
        if (tile == null)
            yield break;

        foreach (var dir in AxialDirections)
        {
            var key = new Vector2Int(tile.q + dir.x, tile.r + dir.y);
            if (_axialLookup.TryGetValue(key, out var neighbor))
                yield return neighbor;
        }
    }
    public Tile GetCenterTile() => _centerTile;
}

