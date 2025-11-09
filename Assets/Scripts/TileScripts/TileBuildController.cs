using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileBuildAction
{
    Grass,
    Bush,
    Tree
}

public class TileBuildController : MonoBehaviour
{
    [Header("Context")]
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileSelectionModel selection;
    [SerializeField] private TileRuntimeStore runtime;
    [SerializeField] private IdleEconomyManager economy;
    [Serializable]
    public struct TileBuildCostSettings
    {
        public TileBuildAction action;
        [Min(0f)] public float startingCost;
        [Min(0f)] public float costMultiplier;

        public TileBuildCostSettings(TileBuildAction action, float startingCost, float costMultiplier)
        {
            this.action = action;
            this.startingCost = startingCost;
            this.costMultiplier = costMultiplier;
        }

        public float GetCostForNext(int builtCount)
        {
            float baseCost = Mathf.Max(0f, startingCost);
            if (builtCount <= 0) return baseCost;

            float mult = Mathf.Max(0f, costMultiplier);
            if (Mathf.Approximately(mult, 0f)) return 0f;
            if (Mathf.Approximately(mult, 1f)) return baseCost;
            return baseCost * Mathf.Pow(mult, builtCount);
        }
    }

    [Header("Cost settings")]
    [SerializeField] private TileBuildCostSettings[] costSettings =
    {
        new TileBuildCostSettings(TileBuildAction.Grass, 0f, 1f),
        new TileBuildCostSettings(TileBuildAction.Bush,  0f, 1f),
        new TileBuildCostSettings(TileBuildAction.Tree,  0f, 1f),
    };

    private readonly Dictionary<TileBuildAction, int> builtCounts = new();
    private readonly Dictionary<TileBuildAction, TileBuildCostSettings> costLookup = new();
    private readonly HashSet<TileBuildAction> missingCostLogged = new();

    [Header("Tile expansion cost")]
    [SerializeField, Min(0f)] private float tileExpansionStartingCost = 1f;
    [SerializeField, Min(0f)] private float tileExpansionCostMultiplier = 5f;

    private static readonly TileBuildAction[] AllActions = (TileBuildAction[])Enum.GetValues(typeof(TileBuildAction));

    public struct TileBuildOption
    {
        public bool canBuild;
        public float cost;
        public string reason;
    }

    // Event informujący inne systemy (UI/telemetria) o zastosowaniu budowy
    public event Action<TileGrid.Tile, TileBuildAction> BuildApplied;

    private void Awake()
    {
        if (!grid) grid = FindAnyObjectByType<TileGrid>();
        if (!selection) selection = FindAnyObjectByType<TileSelectionModel>();
        if (!runtime) runtime = FindAnyObjectByType<TileRuntimeStore>();
        if (!economy) economy = IdleEconomyManager.Instance;

        CacheCostLookup();
        EnsureCountDictionary();
        RecalculateBuiltCounts();
    }

    private void OnValidate()
    {
        CacheCostLookup();
    }

    // ---------- Public API ----------

    public TileBuildOption GetBuildOption(TileGrid.Tile tile, TileBuildAction action)
    {
        TileBuildOption option;
        option.reason = string.Empty;

        float cost = GetNextCost(action);
        string reason;
        bool ok = Evaluate(tile, action, out cost, out reason);

        option.canBuild = ok;
        option.cost = cost;
        option.reason = reason;
        return option;
    }

    public bool TryBuild(TileBuildAction action)
    {
        var tile = selection != null ? selection.Selected : null;
        return TryBuild(action, tile, out _);
    }

    public bool TryBuild(TileBuildAction action, TileGrid.Tile tile, out string failureReason)
    {
        failureReason = string.Empty;

        if (tile == null)
        {
            failureReason = "Brak wybranego kafla.";
            return false;
        }

        if (!Evaluate(tile, action, out var cost, out failureReason))
            return false;

        var wallet = economy ?? IdleEconomyManager.Instance;
        if (cost > 0f)
        {
            if (wallet == null || !wallet.TrySpend(cost))
            {
                failureReason = string.IsNullOrEmpty(failureReason) ? "Za mało pieniędzy." : failureReason;
                return false;
            }
            economy = wallet; // cache
        }

        // 1) Jeśli używasz BuildExecutor do stawiania prefaba i efektów – odkomentuj:
        // executor?.ApplyBuild(<TileRuntime/TileData>, action);

        // 2) Minimalny wariant zgodny z Twoją logiką – tylko kompozycja + liczniki:
        ApplyBuildOnComposition(tile, action);

        // sygnał dla innych systemów
        BuildApplied?.Invoke(tile, action);

        return true;
    }

    // Ekspansja siatki: koszt oparty o liczbę zajętych kafli w runtime store
    public float GetNextTileExpansionCost()
    {
        int occupied = runtime != null ? runtime.OccupiedCount : 0;
        return GetProgressiveCost(tileExpansionStartingCost, tileExpansionCostMultiplier, occupied);
    }

    public TileBuildOption GetTileExpansionOption()
    {
        var opt = new TileBuildOption();
        opt.cost = GetNextTileExpansionCost();
        opt.reason = string.Empty;
        opt.canBuild = HasEnoughFunds(opt.cost, ref opt.reason);
        return opt;
    }

    public bool TrySpendForTileExpansion(out float cost, out string failureReason)
    {
        cost = GetNextTileExpansionCost();
        failureReason = string.Empty;

        if (!HasEnoughFunds(cost, ref failureReason))
            return false;

        if (cost <= 0f)
            return true;

        var wallet = economy ?? IdleEconomyManager.Instance;
        if (wallet == null || !wallet.TrySpend(cost))
        {
            failureReason = string.IsNullOrEmpty(failureReason) ? "Za mało pieniędzy." : failureReason;
            return false;
        }

        economy = wallet;
        return true;
    }

    // ---------- Reguły i koszty ----------

    public bool Evaluate(TileGrid.Tile tile, TileBuildAction action, out float cost, out string failureReason)
    {
        failureReason = string.Empty;
        cost = GetNextCost(action);

        if (tile == null)
        {
            failureReason = "Brak kafla.";
            return false;
        }

        // 1) Reguły kompozycji – tak jak miałeś:
        if (!ValidateComposition(tile, action, ref failureReason))
            return false;

        // 2) Fundusze:
        if (!HasEnoughFunds(cost, ref failureReason))
            return false;

        // 3) (opcjonalnie) dodatkowe reguły zasięgu/sąsiedztwa można podłączyć tutaj

        return true;
    }

    private bool ValidateComposition(TileGrid.Tile tile, TileBuildAction action, ref string failureReason)
    {
        var c = tile.composition;

        switch (action)
        {
            case TileBuildAction.Grass:
                if (c.levelGrass >= 3)
                {
                    failureReason = "Trawa na maks poziomie.";
                    return false;
                }
                return true;

            case TileBuildAction.Bush:
                if (c.levelGrass <= 0)
                {
                    failureReason = "Najpierw posiej trawę.";
                    return false;
                }
                if (c.hasBush && c.bushLevel >= 3)
                {
                    failureReason = "Krzak na maks poziomie.";
                    return false;
                }
                return true;

            case TileBuildAction.Tree:
                if (c.levelGrass < 2)
                {
                    failureReason = "Potrzeba gęstej trawy.";
                    return false;
                }
                if (c.hasTree && c.treeLevel >= 3)
                {
                    failureReason = "Drzewo na maks poziomie.";
                    return false;
                }
                // UWAGA: nie używamy tile.occupied – to jest teraz w runtime store
                var r = runtime != null ? runtime.Get(tile) : null;
                if (r != null && r.occupied)
                {
                    failureReason = "Kafelek już zajęty.";
                    return false;
                }
                return true;

            default:
                failureReason = "Nieznana akcja.";
                return false;
        }
    }

    private bool HasEnoughFunds(float cost, ref string failureReason)
    {
        if (cost <= 0f) return true;
        var wallet = economy ?? IdleEconomyManager.Instance;
        if (wallet != null && wallet.Currency >= cost) return true;

        failureReason = string.IsNullOrEmpty(failureReason) ? "Za mało pieniędzy." : failureReason;
        return false;
    }

    private float GetNextCost(TileBuildAction action)
    {
        int built = GetBuiltCount(action);
        if (!costLookup.TryGetValue(action, out var settings))
        {
            if (!missingCostLogged.Contains(action))
            {
                Debug.LogWarning($"[TileBuildController] Brak konfiguracji kosztu dla akcji {action}.", this);
                missingCostLogged.Add(action);
            }
            return 0f;
        }
        return settings.GetCostForNext(built);
    }

    // ---------- Aktualizacja kompozycji + liczniki ----------

    private void ApplyBuildOnComposition(TileGrid.Tile tile, TileBuildAction action)
    {
        switch (action)
        {
            case TileBuildAction.Grass:
                tile.composition.levelGrass = Mathf.Clamp(tile.composition.levelGrass + 1, 0, 3);
                tile.composition.Validate();
                IncrementBuiltCount(TileBuildAction.Grass);
                break;

            case TileBuildAction.Bush:
                int newBushLevel = tile.composition.hasBush ? tile.composition.bushLevel + 1 : 1;
                newBushLevel = Mathf.Clamp(newBushLevel, 1, 3);
                tile.composition.hasBush = true;
                tile.composition.bushLevel = newBushLevel;
                tile.composition.Validate();
                IncrementBuiltCount(TileBuildAction.Bush);
                break;

            case TileBuildAction.Tree:
                int newTreeLevel = tile.composition.hasTree ? tile.composition.treeLevel + 1 : 1;
                newTreeLevel = Mathf.Clamp(newTreeLevel, 1, 3);
                tile.composition.hasTree = true;
                tile.composition.treeLevel = newTreeLevel;
                tile.composition.Validate();
                IncrementBuiltCount(TileBuildAction.Tree);

                // Jeżeli chcesz od razu oznaczyć runtime jako zajęty (bez stawiania prefabu):
                var r = runtime != null ? runtime.Get(tile) : null;
                if (r != null && !r.occupied)
                {
                    runtime.MarkOccupied(tile, inst: null, template: null);
                }
                break;
        }
    }

    // ---------- Liczniki i koszty ----------

    private void EnsureCountDictionary()
    {
        for (int i = 0; i < AllActions.Length; i++)
        {
            var action = AllActions[i];
            if (!builtCounts.ContainsKey(action))
                builtCounts[action] = 0;
        }
    }

    private void RecalculateBuiltCounts()
    {
        EnsureCountDictionary();
        CacheCostLookup();
        builtCounts[TileBuildAction.Grass] = 0;
        builtCounts[TileBuildAction.Bush]  = 0;
        builtCounts[TileBuildAction.Tree]  = 0;

        if (!grid) return;

        // zliczamy po kompozycjach kafli (tak jak wcześniej)
        for (int i = 0; i < grid.tiles.Count; i++)
        {
            var tile = grid.tiles[i];
            if (tile == null) continue;

            builtCounts[TileBuildAction.Grass] += Mathf.Clamp(tile.composition.levelGrass, 0, 3);
            if (tile.composition.hasBush)
                builtCounts[TileBuildAction.Bush] += Mathf.Clamp(tile.composition.bushLevel, 1, 3);
            if (tile.composition.hasTree)
                builtCounts[TileBuildAction.Tree] += Mathf.Clamp(tile.composition.treeLevel, 1, 3);
        }
    }

    private int GetBuiltCount(TileBuildAction action)
    {
        return builtCounts.TryGetValue(action, out var value) ? value : 0;
    }

    private void IncrementBuiltCount(TileBuildAction action)
    {
        EnsureCountDictionary();
        builtCounts[action] = GetBuiltCount(action) + 1;
    }

    private void CacheCostLookup()
    {
        costLookup.Clear();
        missingCostLogged.Clear();

        if (costSettings == null) return;
        for (int i = 0; i < costSettings.Length; i++)
        {
            var s = costSettings[i];
            costLookup[s.action] = s;
        }
    }

    private static float GetProgressiveCost(float startingCost, float multiplier, int builtCount)
    {
        float baseCost = Mathf.Max(0f, startingCost);
        if (builtCount <= 0) return baseCost;

        float applied = Mathf.Max(0f, multiplier);
        if (Mathf.Approximately(applied, 0f)) return 0f;
        if (Mathf.Approximately(applied, 1f)) return baseCost;
        return baseCost * Mathf.Pow(applied, builtCount);
    }
}