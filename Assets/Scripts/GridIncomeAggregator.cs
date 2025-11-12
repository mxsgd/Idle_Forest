using UnityEngine;

[RequireComponent(typeof(TileGrid))]
public class GridIncomeAggregator : MonoBehaviour, IIncomeSource
{
    private TileGrid grid;
    private float cachedTotal;
    private bool registeredWithEconomy;

    private readonly System.Collections.Generic.Dictionary<TileGrid.Tile, float> perTileIncome =
        new System.Collections.Generic.Dictionary<TileGrid.Tile, float>();

    public float IncomePerTick => cachedTotal;

    private void Awake()
    {
        grid = GetComponent<TileGrid>();
    }

    private void OnEnable()
    {
        TileEvents.CompositionChanged += OnCompositionChanged;
        IdleEconomyManager.InstanceChanged += OnEconomyInstanceChanged;

        TryRegisterWithEconomy(IdleEconomyManager.Instance);
        StartCoroutine(DeferredRebuild());
    }

    private void OnDisable()
    {
        IdleEconomyManager.InstanceChanged -= OnEconomyInstanceChanged;
        TileEvents.CompositionChanged -= OnCompositionChanged;
        UnregisterFromEconomy();

        perTileIncome.Clear();
        cachedTotal = 0f;
    }

    private System.Collections.IEnumerator DeferredRebuild()
    {
        yield return null;
        RebuildAll();
    }

    private void OnCompositionChanged(TileGrid.Tile tile, TileBuildAction _)
    {
        if (tile == null) return;

        float oldInc = perTileIncome.TryGetValue(tile, out var v) ? v : 0f;
        float newInc = ComputeTileIncome(tile);

        perTileIncome[tile] = newInc;
        cachedTotal += (newInc - oldInc);
    }

    private void RebuildAll()
    {
        perTileIncome.Clear();
        cachedTotal = 0f;

        if (grid?.tiles == null) return;
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
        return c != null ? c.GetSumIncome() : 0f;
    }

    private void OnEconomyInstanceChanged(IdleEconomyManager manager)
    {
        if (manager != null) TryRegisterWithEconomy(manager);
        else registeredWithEconomy = false;
    }

    private void TryRegisterWithEconomy(IdleEconomyManager manager)
    {
        if (registeredWithEconomy || manager == null) return;
        if (manager.RegisterIncomeSource(this))
            registeredWithEconomy = true;
    }

    private void UnregisterFromEconomy()
    {
        if (!registeredWithEconomy) return;
        var m = IdleEconomyManager.Instance;
        if (m != null) m.UnregisterIncomeSource(this);
        registeredWithEconomy = false;
    }
}
