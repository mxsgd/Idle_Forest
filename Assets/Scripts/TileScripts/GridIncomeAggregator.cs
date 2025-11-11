using UnityEngine;

[RequireComponent(typeof(TileGrid))]
public class GridIncomeAggregator : MonoBehaviour, IIncomeSource
{
    private TileGrid grid;
    private float cachedTotal;
    private bool initialized;

    public float IncomePerTick => cachedTotal;

    private void Awake()
    {
        grid = GetComponent<TileGrid>();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        TileEvents.CompositionChanged += OnCompositionChanged;
        IdleEconomyManager.Instance?.RegisterIncomeSource(this);
    }

    private void OnDisable()
    {
        TileEvents.CompositionChanged -= OnCompositionChanged;
        IdleEconomyManager.Instance?.UnregisterIncomeSource(this);
    }

    private void InitializeIfNeeded()
    {
        if (initialized) return;
        if (grid == null || grid.tiles == null) return;

        RebuildAll();
        initialized = true;
    }

    private void OnCompositionChanged(TileGrid.Tile tile, TileBuildAction _)
    {
        if (!initialized) InitializeIfNeeded();
        if (tile == null) return;

        float oldInc = GetTileIncomeCached(tile);
        float newInc = ComputeTileIncome(tile);
        cachedTotal += (newInc - oldInc);
        SetTileIncomeCache(tile, newInc);
    }

    private readonly System.Collections.Generic.Dictionary<TileGrid.Tile, float> perTileIncome =
        new System.Collections.Generic.Dictionary<TileGrid.Tile, float>();

    private float GetTileIncomeCached(TileGrid.Tile t) =>
        perTileIncome.TryGetValue(t, out var v) ? v : 0f;

    private void SetTileIncomeCache(TileGrid.Tile t, float v) =>
        perTileIncome[t] = v;

    private void RebuildAll()
    {
        perTileIncome.Clear();
        cachedTotal = 0f;
        for (int i = 0; i < grid.tiles.Count; i++)
        {
            var t = grid.tiles[i];
            if (t == null) continue;
            float inc = ComputeTileIncome(t);
            perTileIncome[t] = inc;
            cachedTotal += inc;
        }
    }

    private static float ComputeTileIncome(TileGrid.Tile tile)
    {
        var c = tile.composition;
        if (c == null) return 0f;
        return c.GetSumIncome();
    }
}