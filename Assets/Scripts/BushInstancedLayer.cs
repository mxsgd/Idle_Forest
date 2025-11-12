using UnityEngine;
using System.Collections.Generic;
using Tile = TileGrid.Tile;

[DefaultExecutionOrder(50)]
public class BushInstancedTileLayer : MonoBehaviour
{
    [Header("Źródło assetów")]
    public GameObject bushPrefab;
    public Mesh bushMesh;
    public Material bushMaterial;

    [Header("Parametry kafla / siatki")]
    public float tileRadius = 0.5f;
    public int seed = 424242;

    [Header("Scatter per poziom (0..3)")]
    public Vector2Int[] perLevelCount = new Vector2Int[]
    {
        new Vector2Int(0, 0),
        new Vector2Int(3, 6),
        new Vector2Int(6, 10),
        new Vector2Int(10, 16),
    };

    [Header("Skalowanie")]
    public Vector2 uniformScaleRange = new Vector2(0.9f, 1.3f);

    [Tooltip("Mnożniki skali dla poziomów 0..3.")]
    public float[] scaleMultiplierByLevel = new float[] { 0f, 1.0f, 1.2f, 1.45f };

    [Header("Offset / rotacja")]
    public float yOffset = 0f;
    public Vector3 meshRotationFixEuler = new Vector3(0, 0, 90);
    private Quaternion _meshRotationFix = Quaternion.identity;

    [Header("Wzrost (animacja)")]
    public float growthDuration = 0.6f;
    public float spawnStagger = 0.25f;

    [Tooltip("Warstwa renderu")]
    public int renderLayer = 0;

    class Batch
    {
        public Vector3[] pos;
        public Quaternion[] rot;
        public float[] targetScale;
        public float[] startTime;
        public Matrix4x4[] matrices;
    }

    private readonly Dictionary<Tile, Batch> _batches = new();
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _meshRotationFix = Quaternion.Euler(meshRotationFixEuler);

        if (bushPrefab && (!bushMesh || !bushMaterial))
        {
            var mf = bushPrefab.GetComponentInChildren<MeshFilter>();
            var mr = bushPrefab.GetComponentInChildren<MeshRenderer>();
            if (mf) bushMesh = mf.sharedMesh;
            if (mr) bushMaterial = new Material(mr.sharedMaterial);
        }

        if (bushMaterial) bushMaterial.enableInstancing = true;
    }

    public void RegenerateFor(Tile tile)
    {
        if (tile == null)
            return;

        int bushLevel = tile.composition.bushLevel;

        if (bushLevel <= 0)
        {
            RemoveFor(tile);
            return;
        }

        if (bushMesh == null || bushMaterial == null) return;

        int lvl = Mathf.Clamp(bushLevel, 1, 3);

        Vector2Int countRange = (perLevelCount != null && perLevelCount.Length > lvl)
            ? perLevelCount[lvl]
            : new Vector2Int(0, 0);

        var rngTarget = new System.Random(tile.q * 9157 ^ tile.r * 23197 ^ seed ^ (lvl * 1013));
        int targetCount = Mathf.Clamp(rngTarget.Next(countRange.x, countRange.y + 1), 0, 2046);

        _batches.TryGetValue(tile, out var b);
        int existing = (b != null) ? b.matrices.Length : 0;

        if (targetCount> existing)
        {
            int toAdd = targetCount - existing;
            AppendInstances(tile, lvl, toAdd);
        }
    }
    private void AppendInstances(Tile tile, int lvl, int addCount)
    {
        if (addCount <= 0) return;

        _batches.TryGetValue(tile, out var oldBatch);
        int existing = (oldBatch != null) ? oldBatch.matrices.Length : 0;
        int newTotal = existing + addCount;

        var newBatch = new Batch
        {
            pos         = new Vector3[newTotal],
            rot         = new Quaternion[newTotal],
            targetScale = new float[newTotal],
            startTime   = new float[newTotal],
            matrices    = new Matrix4x4[newTotal]
        };

        if (existing > 0)
        {
            System.Array.Copy(oldBatch.pos,         newBatch.pos,         existing);
            System.Array.Copy(oldBatch.rot,         newBatch.rot,         existing);
            System.Array.Copy(oldBatch.targetScale, newBatch.targetScale, existing);
            System.Array.Copy(oldBatch.startTime,   newBatch.startTime,   existing);
            System.Array.Copy(oldBatch.matrices,    newBatch.matrices,    existing);
        }

        Vector2Int countRange = (perLevelCount != null && perLevelCount.Length > lvl)
            ? perLevelCount[lvl]
            : new Vector2Int(0, 0);

        float scaleMul = (scaleMultiplierByLevel != null && scaleMultiplierByLevel.Length > lvl)
            ? scaleMultiplierByLevel[lvl]
            : 1f;

        var rng = new System.Random(tile.q * 9157 ^ tile.r * 23197 ^ seed ^ (lvl * 1013) ^ (existing * 49157));

        for (int i = existing; i < newTotal; i++)
        {
            var p = RandomPointInHex(tile.worldPos, tileRadius, rng);
            p.y += yOffset;

            var r = _meshRotationFix;
            float baseScale = Mathf.Lerp(uniformScaleRange.x, uniformScaleRange.y, (float)rng.NextDouble());
            float s = baseScale * scaleMul;

            newBatch.pos[i]         = p;
            newBatch.rot[i]         = r;
            newBatch.targetScale[i] = s;

            newBatch.startTime[i]   = Time.time + Mathf.Lerp(0f, spawnStagger, (float)rng.NextDouble());
            newBatch.matrices[i]    = Matrix4x4.TRS(p, r, Vector3.zero);
        }

        _batches[tile] = newBatch;
    }
    public void RemoveFor(Tile tile)
    {
        if (tile == null) return;
        _batches.Remove(tile);
    }

    private void LateUpdate()
    {
        if (!bushMesh || !bushMaterial || _batches.Count == 0) return;

        float now = Time.time;

        foreach (var kvp in _batches)
        {
            var b = kvp.Value;
            int total = b.matrices.Length;

            for (int i = 0; i < total; i++)
            {
                float t = Mathf.InverseLerp(b.startTime[i], b.startTime[i] + growthDuration, now);
                t = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
                float s = b.targetScale[i] * t;
                b.matrices[i] = Matrix4x4.TRS(b.pos[i], b.rot[i], new Vector3(s, s, s));
            }

            for (int offset = 0; offset < total;)
            {
                int batchCount = Mathf.Min(1023, total - offset);
                Graphics.DrawMeshInstanced(
                    bushMesh, 0, bushMaterial,
                    new System.ArraySegment<Matrix4x4>(b.matrices, offset, batchCount).ToArray(),
                    batchCount, _mpb,
                    UnityEngine.Rendering.ShadowCastingMode.Off, false,
                    renderLayer, null,
                    UnityEngine.Rendering.LightProbeUsage.Off, null
                );
                offset += batchCount;
            }
        }
    }

    // ——— helpers ———
    static Vector3 RandomPointInHex(Vector3 center, float radius, System.Random rng)
    {
        for (int k = 0; k < 8; k++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * radius;
            float z = (float)(rng.NextDouble() * 2 - 1) * radius;
            if (InsideHex(x, z, radius))
                return center + new Vector3(x, 0f, z);
        }
        float a = (float)(rng.NextDouble() * Mathf.PI * 2);
        float r = radius * Mathf.Sqrt((float)rng.NextDouble());
        return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    static bool InsideHex(float x, float z, float r)
    {
        x = Mathf.Abs(x); z = Mathf.Abs(z);
        return (x <= r) && (0.5f * x + 0.8660254f * z <= r);
    }
}
