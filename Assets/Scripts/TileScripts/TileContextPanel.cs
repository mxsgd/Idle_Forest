using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class TileContextPanel : MonoBehaviour
{
    [SerializeField] private TileClickSelector selector;
    [SerializeField] private TileBuildController buildController;

    [SerializeField, Tooltip("Canvas group that is toggled when the panel is shown or hidden.")]
    private CanvasGroup panelCanvasGroup;

    [Header("Buttons")]
    [SerializeField] private Button grassButton;
    [SerializeField] private Button bushButton;
    [SerializeField] private Button treeButton;

    [Header("Ceny")]
    [SerializeField] private TMP_Text grassPriceLabel;
    [SerializeField] private TMP_Text bushPriceLabel;
    [SerializeField] private TMP_Text treePriceLabel;

    private IdleEconomyManager economy;
    private TileGrid.Tile currentTile;

    private void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        HidePanel();
    }

    private void OnEnable()
    {
        IdleEconomyManager.InstanceChanged += OnEconomyInstanceChanged;
        AttachEconomy(IdleEconomyManager.Instance);

        if (selector != null)
            selector.TileSelected += OnTileSelected;

        TileEvents.CompositionChanged += OnAnyCompositionChanged;
    }

    private void OnDisable()
    {
        TileEvents.CompositionChanged -= OnAnyCompositionChanged;
        if (selector != null)
            selector.TileSelected -= OnTileSelected;

        IdleEconomyManager.InstanceChanged -= OnEconomyInstanceChanged;
        DetachEconomy();

        currentTile = null;
        HidePanel();
    }

    private void OnCurrencyChanged(float _)
    {
        RefreshOptions();
    }

    private void OnTileSelected(TileGrid.Tile tile)
    {
        currentTile = tile;

        if (tile == null)
        {
            HidePanel();
            return;
        }

        ShowPanel();
        RefreshOptions();
    }

    private void HandleBuild(TileBuildAction action)
    {
        if (buildController == null || currentTile == null)
            return;

        if (buildController.TryBuild(action, currentTile, out var failureReason))
        {
            RefreshOptions();
        }
        else if (!string.IsNullOrEmpty(failureReason))
        {
            Debug.LogWarning(failureReason);
            RefreshOptions();
        }
    }

    public void RequestBuild(TileBuildAction action)
    {
        HandleBuild(action);
    }

    private void RefreshOptions()
    {
        if (buildController == null || currentTile == null)
            return;

        ApplyOption(grassButton, grassPriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Grass));
        ApplyOption(bushButton, bushPriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Bush));
        ApplyOption(treeButton, treePriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Tree));
    }

    private void ApplyOption(Button button, TMP_Text label, TileBuildController.TileBuildOption option)
    {
        if (button != null)
            button.interactable = option.canBuild;

        if (label != null)
        {
            string priceText = option.cost > 0f ? option.cost.ToString("N0") : "0";
            if (!option.canBuild && !string.IsNullOrEmpty(option.reason))
                priceText += $"\n{option.reason}";

            label.text = priceText;
        }
    }
    private void OnEconomyInstanceChanged(IdleEconomyManager manager)
    {
        AttachEconomy(manager);
    }

    private void AttachEconomy(IdleEconomyManager manager)
    {
        if (economy == manager)
            return;

        DetachEconomy();
        economy = manager;

        if (economy != null)
        {
            economy.CurrencyChanged += OnCurrencyChanged;
            OnCurrencyChanged(economy.Currency);
        }
    }

    private void DetachEconomy()
    {
        if (economy == null)
            return;

        economy.CurrencyChanged -= OnCurrencyChanged;
        economy = null;
    }

    private void OnAnyCompositionChanged(TileGrid.Tile _, TileBuildAction __)
    {
        if (currentTile != null)
            RefreshOptions();
    }

    private void ShowPanel()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void HidePanel()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }
}