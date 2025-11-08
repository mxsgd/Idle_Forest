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
    [SerializeField] private TileGrid grid;
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
            if (builtCount <= 0)
                return baseCost;

            float multiplier = Mathf.Max(0f, costMultiplier);
            if (Mathf.Approximately(multiplier, 0f))
                return 0f;

            if (Mathf.Approximately(multiplier, 1f))
                return baseCost;

            return baseCost * Mathf.Pow(multiplier, builtCount);
        }
    }

    [Header("Cost settings")]
    [SerializeField] private TileBuildCostSettings[] costSettings =
    {
        new TileBuildCostSettings(TileBuildAction.Grass, 0f, 1f),
        new TileBuildCostSettings(TileBuildAction.Bush, 0f, 1f),
        new TileBuildCostSettings(TileBuildAction.Tree, 0f, 1f)
    };

    private readonly Dictionary<TileBuildAction, int> builtCounts = new Dictionary<TileBuildAction, int>();
    private readonly Dictionary<TileBuildAction, TileBuildCostSettings> costLookup = new Dictionary<TileBuildAction, TileBuildCostSettings>();
    private readonly HashSet<TileBuildAction> missingCostLogged = new HashSet<TileBuildAction>();

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

    private readonly HashSet<TileGrid.Tile> expandedTiles = new HashSet<TileGrid.Tile>();
    private int builtTileCount;

    private void OnValidate()
    {
        CacheCostLookup();
    }

    private void Awake()
    {
        if (grid == null)
            grid = GetComponent<TileGrid>();

        if (economy == null)
            economy = IdleEconomyManager.Instance;

        CacheCostLookup();
        EnsureCountDictionary();
        RecalculateBuiltCounts();
        SyncExpandedTiles();
    }
    private void OnEnable()
    {
        if (grid == null)
            grid = GetComponent<TileGrid>();

        if (grid != null)
        {
            grid.TileStateChanged += HandleTileStateChanged;
            SyncExpandedTiles();
        }
    }

    private void OnDisable()
    {
        if (grid != null)
            grid.TileStateChanged -= HandleTileStateChanged;
    }
    public TileBuildOption GetBuildOption(TileGrid.Tile tile, TileBuildAction action)
    {
        TileBuildOption option;
        option.cost = 0f;
        option.reason = string.Empty;
        option.canBuild = Evaluate(tile, action, out option.cost, out option.reason);
        return option;
    }

    public bool TryBuild(TileBuildAction action)
    {
        var targetTile = grid != null ? grid.SelectedTile : null;
        return TryBuild(action, targetTile, out _);
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

        if (cost > 0f)
        {
            var targetEconomy = economy != null ? economy : IdleEconomyManager.Instance;
            if (targetEconomy == null || !targetEconomy.TrySpend(cost))
            {
                failureReason = string.IsNullOrEmpty(failureReason) ? "Za mało pieniędzy." : failureReason;
                return false;
            }

            economy = targetEconomy;
        }

        ApplyBuild(tile, action);
        return true;
    }

    public float GetNextTileExpansionCost()
    {
        return GetProgressiveCost(tileExpansionStartingCost, tileExpansionCostMultiplier, builtTileCount);
    }

    public TileBuildOption GetTileExpansionOption()
    {
        TileBuildOption option;
        option.cost = GetNextTileExpansionCost();
        option.reason = string.Empty;
        option.canBuild = HasEnoughFunds(option.cost, ref option.reason);
        return option;
    }

    public bool TrySpendForTileExpansion(out float cost, out string failureReason)
    {
        cost = GetNextTileExpansionCost();
        failureReason = string.Empty;

        if (!HasEnoughFunds(cost, ref failureReason))
            return false;

        if (cost <= 0f)
            return true;

        var targetEconomy = economy != null ? economy : IdleEconomyManager.Instance;
        if (targetEconomy == null || !targetEconomy.TrySpend(cost))
        {
            failureReason = string.IsNullOrEmpty(failureReason) ? "Za mało pieniędzy." : failureReason;
            return false;
        }

        economy = targetEconomy;
        return true;
    }


    public bool Evaluate(TileGrid.Tile tile, TileBuildAction action, out float cost, out string failureReason)
    {
        cost = GetNextCost(action);
        failureReason = string.Empty;

        if (tile == null)
        {
            failureReason = "Brak kafla.";
            return false;
        }

        var composition = tile.composition;

        switch (action)
        {
            case TileBuildAction.Grass:
                if (composition.levelGrass >= 3)
                {
                    failureReason = "Trawa na maks poziomie.";
                    return false;
                }

                return HasEnoughFunds(cost, ref failureReason);

            case TileBuildAction.Bush:
                if (composition.levelGrass <= 0)
                {
                    failureReason = "Najpierw posiej trawę.";
                    return false;
                }

                if (composition.hasBush && composition.bushLevel >= 3)
                {
                    failureReason = "Krzak na maks poziomie.";
                    return false;
                }

                return HasEnoughFunds(cost, ref failureReason);

            case TileBuildAction.Tree:
                if (composition.levelGrass < 2)
                {
                    failureReason = "Potrzeba gęstej trawy.";
                    return false;
                }

                if (composition.hasTree && composition.treeLevel >= 3)
                {
                    failureReason = "Drzewo na maks poziomie.";
                    return false;
                }

                return HasEnoughFunds(cost, ref failureReason);

            default:
                failureReason = "Nieznana akcja.";
                return false;
        }
    }

    private bool HasEnoughFunds(float cost, ref string failureReason)
    {
        if (cost <= 0f)
            return true;

        var targetEconomy = economy != null ? economy : IdleEconomyManager.Instance;
        if (targetEconomy != null && targetEconomy.Currency >= cost)
            return true;

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

    private void ApplyBuild(TileGrid.Tile tile, TileBuildAction action)
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
                tile.occupied = true;
                IncrementBuiltCount(TileBuildAction.Tree);
                break;
        }
        
        if (grid != null)
            grid.NotifyTileChanged(tile);
    }

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
        builtCounts[TileBuildAction.Bush] = 0;
        builtCounts[TileBuildAction.Tree] = 0;

        if (grid == null)
            return;

        for (int i = 0; i < grid.tiles.Count; i++)
        {
            var tile = grid.tiles[i];
            if (tile == null)
                continue;

            builtCounts[TileBuildAction.Grass] += Mathf.Clamp(tile.composition.levelGrass, 0, 3);
            if (tile.composition.hasBush)
                builtCounts[TileBuildAction.Bush] += Mathf.Clamp(tile.composition.bushLevel, 1, 3);
            if (tile.composition.hasTree)
                builtCounts[TileBuildAction.Tree] += Mathf.Clamp(tile.composition.treeLevel, 1, 3);
        }
        SyncExpandedTiles();
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

        if (costSettings == null)
            return;

        for (int i = 0; i < costSettings.Length; i++)
        {
            var settings = costSettings[i];
            costLookup[settings.action] = settings;
        }
    }
    
    private void SyncExpandedTiles()
    {
        expandedTiles.Clear();
        builtTileCount = 0;

        if (grid == null || grid.tiles == null)
            return;

        for (int i = 0; i < grid.tiles.Count; i++)
        {
            var tile = grid.tiles[i];
            if (tile == null || !tile.occupied)
                continue;

            expandedTiles.Add(tile);
        }

        builtTileCount = expandedTiles.Count;
    }

    private void HandleTileStateChanged(TileGrid.Tile tile)
    {
        if (tile == null)
            return;

        if (tile.occupied)
        {
            if (expandedTiles.Add(tile))
                builtTileCount = expandedTiles.Count;
        }
        else
        {
            if (expandedTiles.Remove(tile))
                builtTileCount = expandedTiles.Count;
        }
    }

    private static float GetProgressiveCost(float startingCost, float multiplier, int builtCount)
    {
        float baseCost = Mathf.Max(0f, startingCost);
        if (builtCount <= 0)
            return baseCost;

        float appliedMultiplier = Mathf.Max(0f, multiplier);
        if (Mathf.Approximately(appliedMultiplier, 0f))
            return 0f;

        if (Mathf.Approximately(appliedMultiplier, 1f))
            return baseCost;

        return baseCost * Mathf.Pow(appliedMultiplier, builtCount);
    }
}
