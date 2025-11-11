using UnityEngine;
using System.Collections.Generic;
using Tile = TileGrid.Tile;

[DefaultExecutionOrder(50)]
public class GrassInstancedTileLayer : MonoBehaviour
{
    [Header("Źródło assetów (podaj JEDNO)")]
    [Tooltip("Jeśli ustawisz prefab, skrypt sam wyciągnie Mesh i Material")]
    public GameObject grassPrefab;
    public Mesh grassMesh;
    public Material grassMaterial;

    [Header("Parametry kafla / siatki")]
    [Tooltip("Promień heksa w jednostkach świata")]
    public float tileRadius = 0.5f;
    [Tooltip("Stały seed, żeby rozkład był powtarzalny")]
    public int seed = 1337;

    [Header("Scatter")]
    public Vector2Int perTileCount = new Vector2Int(6, 12);
    public Vector2 uniformScaleRange = new Vector2(0.8f, 1.2f);
    public float yOffset = 0f;
    public Vector3 meshRotationFixEuler = new Vector3(0,0,90); // np. (-90,0,0) jeśli mesh jest „na boku”
    private Quaternion _meshRotationFix = Quaternion.identity;

    [Header("Wzrost (animacja)")]
    public float growthDuration = 0.6f;
    public float spawnStagger = 0.25f;
    [Tooltip("Warstwa renderu (opcjonalnie)")]
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

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _meshRotationFix = Quaternion.Euler(meshRotationFixEuler);
        if (grassPrefab && (!grassMesh || !grassMaterial))
        {
            var mf = grassPrefab.GetComponentInChildren<MeshFilter>();
            var mr = grassPrefab.GetComponentInChildren<MeshRenderer>();
            if (mf) grassMesh = mf.sharedMesh;
            if (mr) grassMaterial = new Material(mr.sharedMaterial);
        }

        if (grassMaterial) grassMaterial.enableInstancing = true;
    }

    void OnEnable()
    {

    }

    void OnDisable()
    {

    }

    public void RegenerateFor(Tile tile)
    {
        if (tile == null || grassMesh == null || grassMaterial == null) return;

        var rng = new System.Random(tile.q * 73856093 ^ tile.r * 19349663 ^ seed);
        int count = Mathf.Clamp(rng.Next(perTileCount.x, perTileCount.y + 1), 0, 2046);

        var b = new Batch
        {
            pos = new Vector3[count],
            rot = new Quaternion[count],
            targetScale = new float[count],
            startTime = new float[count],
            matrices = new Matrix4x4[count]
        };

        for (int i = 0; i < count; i++)
        {
            var p = RandomPointInHex(tile.worldPos, tileRadius, rng);
            p.y += yOffset;

            // var r = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f);
            var r = _meshRotationFix;
            var s = Mathf.Lerp(uniformScaleRange.x, uniformScaleRange.y, (float)rng.NextDouble());

            b.pos[i] = p;
            b.rot[i] = r;
            b.targetScale[i] = s;
            b.startTime[i] = Time.time + Mathf.Lerp(0f, spawnStagger, (float)rng.NextDouble());
            b.matrices[i] = Matrix4x4.TRS(p, r, Vector3.zero);
        }

        _batches[tile] = b;
    }

    public void RemoveFor(Tile tile)
    {
        if (tile == null) return;
        _batches.Remove(tile);
    }

    void LateUpdate()
    {
        if (!grassMesh || !grassMaterial || _batches.Count == 0) return;

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

            // drawing 1023 batches
            for (int offset = 0; offset < total;)
            {
                int batchCount = Mathf.Min(1023, total - offset);
                Graphics.DrawMeshInstanced(
                    grassMesh, 0, grassMaterial,
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
