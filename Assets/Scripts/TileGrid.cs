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

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color tileColor = new Color(0f, 1f, 0.2f, 0.1f);
    public Color occupiedColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color centerTileColor = new Color(0.45f, 0.26f, 0.12f, 0.85f);
    public Color neighborTileColor = Color.white;
    public float gizmoSize = 0.08f;

    [System.Serializable]
    public class Tile
    {
        public int i;
        public int j;
        public int q;
        public int r;
        public Vector3 worldPos;
        public bool occupied;
        public GameObject occupant;
        public TileComposition composition = new TileComposition();

        public TileDensity Density => composition.GetDensity();
        public TileElement DominantElement => composition.GetDominantElement();
        public float Production => composition.GetProduction();
    }

    public List<Tile> tiles = new List<Tile>();

    private Tile[,] _grid;
    private readonly Dictionary<Vector2Int, Tile> _axialLookup = new Dictionary<Vector2Int, Tile>();
    private Tile _centerTile;

    private static readonly Vector2Int[] AxialDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    private static Mesh _hexMesh;
    private float _hexScale = 1f;

    private Bounds _localBounds;

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
        float best = maxDistance;

        for (int k = 0; k < tiles.Count; k++)
        {
            var t = tiles[k];
            if (t.occupied) continue;

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
        tile.occupied = true;
        tile.occupant = occupant;
    }

    public void FreeTile(Tile tile)
    {
        if (tile == null) return;
        tile.occupied = false;
        tile.occupant = null;
    }

    public Tile GetTile(int i, int j) => (i >= 0 && i < rows && j >= 0 && j < cols) ? _grid[i, j] : null;

    void OnDrawGizmos()
    {
        if (!drawGizmos || tiles == null) return;

        EnsureHexMesh();

        var center = _centerTile;
        var neighbors = center != null ? new HashSet<Tile>(GetNeighbors(center)) : null;

        for (int k = 0; k < tiles.Count; k++)
        {
            var t = tiles[k];

            if (t == null) continue;

            if (t.occupied)
            {
                Gizmos.color = occupiedColor;
            }
            else if (t == center)
            {
                Gizmos.color = centerTileColor;
            }
            else if (neighbors != null && neighbors.Contains(t))
            {
                Gizmos.color = neighborTileColor;
            }
            else
            {
                Gizmos.color = tileColor;
            }

            Gizmos.DrawMesh(_hexMesh, t.worldPos, transform.rotation, Vector3.one * _hexScale);
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

    private void EnsureHexMesh()
    {
        if (_hexMesh != null)
            return;

        _hexMesh = new Mesh { name = "TileGridHex" };

        var vertices = new Vector3[7];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        
        var triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = (i + 1) % 6 + 1;
        }

        _hexMesh.vertices = vertices;
        _hexMesh.triangles = triangles;
        _hexMesh.RecalculateNormals();
        _hexMesh.RecalculateBounds();
    }
}

