using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

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
    [SerializeField] private GrassInstancedTileLayer grassLayer;
    [SerializeField] private BushInstancedTileLayer bushLayer;
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
        new TileBuildCostSettings(TileBuildAction.Grass, 5, 2f),
        new TileBuildCostSettings(TileBuildAction.Bush,  25f, 2f),
        new TileBuildCostSettings(TileBuildAction.Tree,  200f, 2f),
    };

    private readonly Dictionary<TileBuildAction, TileBuildCostSettings> costLookup = new();
    private readonly HashSet<TileBuildAction> missingCostLogged = new();

    public struct TileBuildOption
    {
        public bool canBuild;
        public float cost;
        public string reason;
    }

    private void Awake()
    {
        if (!grid) grid = FindAnyObjectByType<TileGrid>();
        if (!selection) selection = FindAnyObjectByType<TileSelectionModel>();
        if (!runtime) runtime = FindAnyObjectByType<TileRuntimeStore>();
        if (!economy) economy = IdleEconomyManager.Instance;

        CacheCostLookup();
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

        float cost;
        string reason;
        bool ok = Evaluate(tile, action, out cost, out reason);

        option.canBuild = ok;
        option.cost = cost;
        option.reason = reason;
        return option;
    }

    public bool TryBuild(TileBuildAction action, TileGrid.Tile tile, out string failureReason)
    {

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
            economy = wallet;
        }

        ApplyBuildOnComposition(tile, action);
        TileEvents.RaiseCompositionChanged(tile, action);

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

        var rt = runtime.Get(tile);
        if(!rt.occupied)
        {
            failureReason = "Brak kafla.";
            return false;
        }

        if (!ValidateComposition(tile, action, ref failureReason))
            return false;

        if (!HasEnoughFunds(cost, ref failureReason))
            return false;


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
                if (c.bushLevel >= 3)
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
                if (c.treeLevel >= 3)
                {
                    failureReason = "Drzewo na maks poziomie.";
                    return false;
                }
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
                grassLayer.RegenerateFor(tile);
                break;

            case TileBuildAction.Bush:
                tile.composition.bushLevel = Mathf.Clamp(tile.composition.bushLevel + 1, 0, 3);
                tile.composition.Validate();
                bushLayer.RegenerateFor(tile);
                break;

            case TileBuildAction.Tree:
                tile.composition.treeLevel = Mathf.Clamp(tile.composition.treeLevel + 1, 0, 3);
                tile.composition.Validate();

                var r = runtime != null ? runtime.Get(tile) : null;
                if (r != null && !r.occupied)
                {
                    runtime.MarkOccupied(tile, inst: null, template: null);
                }
                break;
        }
    }

    private int GetBuiltCount(TileBuildAction action)
    {
        if (!grid || grid.tiles == null) return 0;

        int total = 0;
        for (int i = 0; i < grid.tiles.Count; i++)
        {
            var t = grid.tiles[i];
            if (t == null) continue;
            var c = t.composition;

            switch (action)
            {
                case TileBuildAction.Grass:
                    total += Mathf.Clamp(c.levelGrass, 0, 3);
                    break;

                case TileBuildAction.Bush:
                    total += Mathf.Clamp(c.bushLevel, 0, 3);
                    break;

                case TileBuildAction.Tree:
                    total += Mathf.Clamp(c.treeLevel, 0, 3);
                    break;
            }
        }
        return total;
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
}