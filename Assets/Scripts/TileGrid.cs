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
    public Color tileColor = new Color(0f, 1f, 0.2f, 0.25f);
    public Color occupiedColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    public float gizmoSize = 0.08f;

    [System.Serializable]
    public class Tile
    {
        public int i;            // row
        public int j;            // col
        public Vector3 worldPos; // środek kafla w świecie
        public bool occupied;
        public GameObject occupant;
    }

    // public list — do podglądu/debugu
    public List<Tile> tiles = new List<Tile>();

    // szybki dostęp po indeksach (opcjonalnie)
    private Tile[,] _grid;

    // rozmiary w lokalnej przestrzeni mesha
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
        if (rows < 1 || cols < 1) return;

        _grid = new Tile[rows, cols];

        // lokalne wymiary podłoża
        var sizeLocal = _localBounds.size;
        var minLocal = _localBounds.min;

        // rozmiar kafla w lokalnej przestrzeni
        float stepX = sizeLocal.x / cols;
        float stepZ = sizeLocal.z / rows;

        // generujemy środki kafli w lokalnej, potem transformujemy do świata
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                // środek kafla w lokalnej
                float cx = minLocal.x + (j + 0.5f) * stepX;
                float cz = minLocal.z + (i + 0.5f) * stepZ;
                Vector3 localCenter = new Vector3(cx, _localBounds.center.y, cz);

                // do świata + „przyklejenie” do powierzchni collidera raycastem od góry
                Vector3 worldCenter = transform.TransformPoint(localCenter);
                Vector3 rayStart = worldCenter + transform.up * 5f;

                if (Physics.Raycast(rayStart, -transform.up, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                {
                    worldCenter = hit.point; // środek na prawdziwej powierzchni (skalowany/rotowany plane/mesh)
                }

                var t = new Tile
                {
                    i = i,
                    j = j,
                    worldPos = worldCenter,
                    occupied = false,
                    occupant = null
                };

                tiles.Add(t);
                _grid[i, j] = t;
            }
        }
    }

    public bool TryGetNearestFreeTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        float best = maxDistance;

        // prosta pętla po liście — przy 100–10k kafli jest OK
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

        for (int k = 0; k < tiles.Count; k++)
        {
            var t = tiles[k];
            Gizmos.color = t.occupied ? occupiedColor : tileColor;
            Gizmos.DrawSphere(t.worldPos, gizmoSize);
        }
    }
}

