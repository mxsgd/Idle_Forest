using System;
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
        public int i;
        public int j;
        public int q;
        public int r;
        public Vector3 worldPos;
        public bool occupied;
        public bool available;
        public GameObject occupant;
        public GameObject placedPrefab;
        public TileComposition composition = new TileComposition();

        public TileDensity Density => composition.GetDensity();
        public TileElement DominantElement => composition.GetDominantElement();
        public float Production => composition.GetProduction();
    }

    public List<Tile> tiles = new List<Tile>();

    private Tile[,] _grid;
    private readonly Dictionary<Vector2Int, Tile> _axialLookup = new Dictionary<Vector2Int, Tile>();
    private Tile _centerTile;
    private Tile _selectedTile;
    public event Action<Tile> SelectedTileChanged;
    private int _occupiedCount;
    private static readonly Vector2Int[] AxialDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    private float _hexScale = 1f;
    private Bounds _localBounds;

    public event Action<Tile> TileStateChanged;

    void OnEnable()
    {
        CacheBounds();
        BuildGrid();
    }

    void OnValidate()
    {
        if (!Application.isPlaying && rebuildOnValidate)
        {
            CacheBounds();
            BuildGrid();
        }
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
        _occupiedCount = 0;
        
        if (rows < 1 || cols < 1) return;

        _grid = new Tile[rows, cols];

        var localPositions = new Vector2[rows * cols];

        int index = 0;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                float q = j - (cols - 1) * 0.5f;
                float r = i - (rows - 1) * 0.5f;

                float x = Mathf.Sqrt(3f) * (q + r * 0.5f);
                float z = 1.5f * r;

                localPositions[index] = new Vector2(x, z);

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z);
                maxZ = Mathf.Max(maxZ, z);

                index++;
            }
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

        int roundedQCenter = Mathf.RoundToInt((cols - 1) * 0.5f);
        int roundedRCenter = Mathf.RoundToInt((rows - 1) * 0.5f);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                Vector2 coords = localPositions[index++];

                float scaledX = (coords.x - centerX) * _hexScale;
                float scaledZ = (coords.y - centerZ) * _hexScale;

                Vector3 localCenter = new Vector3(boundsCenter.x + scaledX, boundsCenter.y, boundsCenter.z + scaledZ);

                Vector3 worldCenter = transform.TransformPoint(localCenter);
                Vector3 rayStart = worldCenter + transform.up * 5f;

                if (Physics.Raycast(rayStart, -transform.up, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                {
                    worldCenter = hit.point;
                }

                int axialQ = j - roundedQCenter;
                int axialR = i - roundedRCenter;

                var t = new Tile
                {
                    i = i,
                    j = j,
                    q = axialQ,
                    r = axialR,
                    worldPos = worldCenter,
                    occupied = false,
                    occupant = null
                };
                t.composition.Validate();

                tiles.Add(t);
                _grid[i, j] = t;
                _axialLookup[new Vector2Int(t.q, t.r)] = t;

                float distance = Vector3.SqrMagnitude(worldCenter - gridWorldCenter);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    _centerTile = t;
                }
            }
        }
    }

    public bool TryGetNearestFreeTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        float bestSqr = maxDistance * maxDistance;

        for (int k = 0; k < tiles.Count; k++)
        {
            var t = tiles[k];
            if (t.occupied)
                continue;

            float d = Vector3.SqrMagnitude(worldPoint - t.worldPos);
            if (d < bestSqr)
            {
                bestSqr = d;
                tile = t;
            }
        }
        return tile != null;
    }
    //funkcja do wyboru myszka kafelka
    public bool TryGetNearestTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        float best = maxDistance;

        for (int k = 0; k < tiles.Count; k++)
        {
            var t = tiles[k];
            float d = Vector3.SqrMagnitude(worldPoint - t.worldPos);
            if (d < best * best)
            {
                best = Mathf.Sqrt(d);
                tile = t;
            }
        }

        return tile != null;
    }

    public void MarkOccupied(Tile tile, GameObject occupant)
    {
        if (tile == null) return;
        if (!tile.occupied)
            _occupiedCount++;
        tile.available = false;
        tile.occupied = true;
        tile.occupant = occupant;
        TileStateChanged?.Invoke(tile);

    }
    // Czy funckja FreeTile jest do czegos na pewno potrzebna?
    public void FreeTile(Tile tile)
    {
        if (tile == null) return;
        if (tile.occupied && _occupiedCount > 0)
            _occupiedCount--;
        tile.available = true;
        tile.occupied = false;
        tile.occupant = null;
    }

    public Tile GetTile(int i, int j) => (i >= 0 && i < rows && j >= 0 && j < cols) ? _grid[i, j] : null;

    public Tile SelectedTile => _selectedTile;

    public void SetSelectedTile(Tile tile)
    {
        if (_selectedTile == tile)
            return;

        _selectedTile = tile;
        SelectedTileChanged?.Invoke(_selectedTile);
    }

    public void ClearSelectedTile()
    {
        if (_selectedTile == null)
            return;

        _selectedTile = null;
        SelectedTileChanged?.Invoke(null);
    }

    public GameObject PlaceTile(GameObject tilePrefab, Tile tile = null, Transform parent = null, Quaternion rotation = new Quaternion())
    {
        if (tilePrefab == null)
            return null;

        var targetTile = tile ?? GetCenterTile();
        if (targetTile == null || targetTile.occupied)
            return null;

        if (rotation.Equals(new Quaternion()))
            rotation = Quaternion.identity;

        var instance = Instantiate(tilePrefab, targetTile.worldPos, rotation, parent);
        targetTile.placedPrefab = tilePrefab;

        MarkOccupied(targetTile, instance);

        return instance;
    }
    public IEnumerable<Tile> GetAvailableTiles()
    {
        if (tiles == null || tiles.Count == 0)
            yield break;

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile != null)
                tile.available = false;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile == null || !tile.occupied)
                continue;

            foreach (var neighbor in GetNeighbors(tile))
            {
                if (neighbor == null || neighbor.occupied)
                    continue;

                neighbor.available = true;

                if (neighbor.placedPrefab == null)
                    neighbor.placedPrefab = tile.placedPrefab;
            }
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile != null && tile.available && !tile.occupied)
                yield return tile;
        }
    }
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

