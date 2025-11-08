using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileAvailabilityVisualizer : MonoBehaviour
{
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private Transform availableTileParent;
    [SerializeField] private GameObject fallbackAvailableTilePrefab;
    [SerializeField, Range(0f, 1f)] private float availableAlpha = 0.35f;
    [SerializeField] private string availableTag = "";

    [Header("Selection Highlight")]
    [SerializeField] private Transform selectedTileParent;
    [SerializeField] private GameObject fallbackSelectedTilePrefab;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 0.5f;
    [SerializeField] private string selectedTag = "";
    [SerializeField] private Vector3 selectedOffset = new Vector3(0f, 1f, 0f);

    [Header("Tile Purchase")]
    [SerializeField, Min(0f)] private float purchaseHoldDuration = 2f;
    [SerializeField, Min(0f)] private float purchaseCost = 0f;
    [SerializeField] private IdleEconomyManager economy;
    [SerializeField] private Transform purchasedTileParent;
    [SerializeField] private GameObject fallbackPurchasedTilePrefab;
    [SerializeField] private Camera purchaseCamera;
    [SerializeField] private LayerMask purchaseRaycastMask = ~0;
    [SerializeField] private float purchaseRaycastDistance = 500f;
    [SerializeField] private Vector3 purchasedTileOffset = Vector3.zero;
    [SerializeField] private Vector3 holdProgressOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private GameObject holdProgressPrefab;

    private readonly Dictionary<TileGrid.Tile, GameObject> _availableTiles = new Dictionary<TileGrid.Tile, GameObject>();
    private readonly List<TileGrid.Tile> _removalBuffer = new List<TileGrid.Tile>();
    private TileGrid.Tile _highlightedTile;
    private GameObject _selectedTileInstance;
    private const int MousePointerId = -1;
    private bool _isHoldingToPurchase;
    private float _holdTimer;
    private int _activePointerId = int.MinValue;
    private GameObject _holdProgressInstance;
    private HoldProgressIndicator _holdProgressIndicator;
    private Vector3 _holdProgressBaseScale = Vector3.one;

    private void Awake()
    {
        if (tileGrid == null)
            tileGrid = GetComponent<TileGrid>();

        if (availableTileParent == null && tileGrid != null)
            availableTileParent = tileGrid.transform;


        if (selectedTileParent == null && tileGrid != null)
            selectedTileParent = tileGrid.transform;

        if (purchaseCamera == null)
            purchaseCamera = Camera.main;

        if (economy == null)
            economy = IdleEconomyManager.Instance;
    }

    private void OnEnable()
    {
        if (tileGrid != null)
        {
            tileGrid.TileStateChanged += HandleTileStateChanged;
            tileGrid.SelectedTileChanged += HandleSelectedTileChanged;
        }

        RefreshAvailability();
        UpdateSelectedTileHighlight(tileGrid != null ? tileGrid.SelectedTile : null);
    }

    private void Start()
    {
        RefreshAvailability();
        UpdateSelectedTileHighlight(tileGrid != null ? tileGrid.SelectedTile : null);
    }

    private void OnDisable()
    {
        if (tileGrid != null)
        {
            tileGrid.TileStateChanged -= HandleTileStateChanged;
            tileGrid.SelectedTileChanged -= HandleSelectedTileChanged;
        }

        ClearAvailableTiles();
        ClearSelectedTileHighlight();
        ResetPurchaseHold();
    }

    private void Update()
    {
        UpdatePurchaseHold();
    }

    private void HandleTileStateChanged(TileGrid.Tile _)
    {
        RefreshAvailability();
    }
    private void HandleSelectedTileChanged(TileGrid.Tile tile)
    {
        UpdateSelectedTileHighlight(tile);
    }
    private void RefreshAvailability()
    {
        if (tileGrid == null)
        {
            ClearAvailableTiles();
            return;
        }

        var currentlyAvailable = new HashSet<TileGrid.Tile>();

        foreach (var tile in tileGrid.GetAvailableTiles())
        {
            if (tile == null || tile.occupied)
                continue;

            currentlyAvailable.Add(tile);

            if (_availableTiles.ContainsKey(tile))
                continue;

            var instance = tileGrid.PlaceAvailabilityPrefab(tile, fallbackAvailableTilePrefab, availableTileParent, availableAlpha, availableTag);
            if (instance != null)
                _availableTiles.Add(tile, instance);
        }

        RemoveStaleAvailableTiles(currentlyAvailable);
    }

    private void ClearAvailableTiles()
    {
        foreach (var kvp in _availableTiles)
        {
            if (kvp.Key != null)
                kvp.Key.available = false;

            if (tileGrid != null)
                tileGrid.RemoveAvailabilityPrefab(kvp.Key);
        }

        _availableTiles.Clear();
    }

    private void RemoveStaleAvailableTiles(HashSet<TileGrid.Tile> currentlyAvailable)
    {
        _removalBuffer.Clear();

        foreach (var kvp in _availableTiles)
        {
            var tile = kvp.Key;
            if (!currentlyAvailable.Contains(tile))
                _removalBuffer.Add(tile);
        }

        foreach (var tile in _removalBuffer)
        {
            RemoveAvailableTile(tile);
        }

        _removalBuffer.Clear();
    }

    private void RemoveAvailableTile(TileGrid.Tile tile)
    {
        if (_availableTiles.ContainsKey(tile))
        {
            tile.available = false;

            if (tileGrid != null)
                tileGrid.RemoveAvailabilityPrefab(tile);


            _availableTiles.Remove(tile);
        }
    }
    private void UpdateSelectedTileHighlight(TileGrid.Tile tile)
    {
        if (tile == _highlightedTile)
            return;
        ResetPurchaseHold();

        ClearSelectedTileHighlight();

        if (tile == null)
            return;

        var parent = selectedTileParent != null ? selectedTileParent : (tileGrid != null ? tileGrid.transform : null);
        if (parent == null)
            return;

        var template = fallbackSelectedTilePrefab != null ? fallbackSelectedTilePrefab : fallbackAvailableTilePrefab;
        if (template == null)
            return;

        _selectedTileInstance = Instantiate(template, tile.worldPos + selectedOffset, Quaternion.identity, parent);
        ConfigureSelectionInstance(_selectedTileInstance, selectedAlpha, selectedTag);
        _highlightedTile = tile;
    }

    private void ClearSelectedTileHighlight()
    {
        if (_selectedTileInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(_selectedTileInstance);
        else
            DestroyImmediate(_selectedTileInstance);

        _selectedTileInstance = null;
        _highlightedTile = null;
    }

    private static void ConfigureSelectionInstance(GameObject instance, float alpha, string tag)
    {
        if (instance == null)
            return;

        instance.name = instance.name.Replace("(Clone)", string.Empty).Trim() + " (Selected)";

        if (!string.IsNullOrEmpty(tag))
            instance.tag = tag;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!material.HasProperty("_Color"))
                    continue;

                var color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
    }
    private void UpdatePurchaseHold()
    {
        if (!isActiveAndEnabled)
        {
            ResetPurchaseHold();
            return;
        }

        if (!Application.isPlaying)
        {
            ResetPurchaseHold();
            return;
        }

        if (_highlightedTile == null || tileGrid == null || purchaseHoldDuration <= 0f)
        {
            ResetPurchaseHold();
            return;
        }

        if (!TryGetActivePointer(out var pointerPosition, out var pointerId))
        {
            ResetPurchaseHold();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
        {
            ResetPurchaseHold();
            return;
        }

        if (!IsPointerOverHighlightedTile(pointerPosition))
        {
            ResetPurchaseHold();
            return;
        }

        if (!_isHoldingToPurchase)
            BeginPurchaseHold(pointerId);

        if (!_isHoldingToPurchase)
            return;

        _holdTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(_holdTimer / purchaseHoldDuration);
        UpdateHoldIndicator(progress);

        if (_holdTimer < purchaseHoldDuration)
        {
            UpdateHoldIndicatorPosition();
            return;
        }

        if (TryPurchaseHighlightedTile())
        {
            UpdateHoldIndicator(1f);
        }

        ResetPurchaseHold();
    }

    private void BeginPurchaseHold(int pointerId)
    {
        if (_highlightedTile == null)
            return;

        _isHoldingToPurchase = true;
        _holdTimer = 0f;
        _activePointerId = pointerId;
        CreateHoldIndicator();
        UpdateHoldIndicator(0f);
        UpdateHoldIndicatorPosition();
    }

    private void ResetPurchaseHold()
    {
        _isHoldingToPurchase = false;
        _holdTimer = 0f;
        _activePointerId = int.MinValue;
        DestroyHoldIndicator();
    }

    private bool TryPurchaseHighlightedTile()
    {
        var tile = _highlightedTile;
        if (tileGrid == null || tile == null || tile.occupied)
            return false;

        if (!_availableTiles.ContainsKey(tile))
            return false;

        var prefab = tile.placedPrefab != null ? tile.placedPrefab : fallbackPurchasedTilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[TileAvailabilityVisualizer] Brak prefabu kafelka do postawienia.", this);
            return false;
        }

        IdleEconomyManager targetEconomy = economy != null ? economy : IdleEconomyManager.Instance;

        if (purchaseCost > 0f)
        {
            if (targetEconomy == null || !targetEconomy.TrySpend(purchaseCost))
                return false;

            economy = targetEconomy;
        }

        Transform parent = purchasedTileParent != null ? purchasedTileParent : (tileGrid != null ? tileGrid.transform : null);
        if (parent == null)
        {
            if (purchaseCost > 0f && targetEconomy != null)
                targetEconomy.AddIncome(purchaseCost);
            Debug.LogWarning("[TileAvailabilityVisualizer] Brak rodzica dla kupionego kafelka.", this);
            return false;
        }

        var instance = tileGrid.PlaceTile(prefab, tile, parent, null);
        if (instance == null)
        {
            if (purchaseCost > 0f && targetEconomy != null)
                targetEconomy.AddIncome(purchaseCost);
            Debug.LogWarning("[TileAvailabilityVisualizer] Nie udało się postawić kafelka.", this);
            return false;
        }

        RemoveAvailableTile(tile);
        UpdateSelectedTileHighlight(tile);
        return true;
    }

    private bool TryGetActivePointer(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = int.MinValue;

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (var touch in touchscreen.touches)
            {
                if (touch == null || !touch.press.isPressed)
                    continue;

                position = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();

                if (!_isHoldingToPurchase || pointerId == _activePointerId)
                    return true;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            position = mouse.position.ReadValue();
            pointerId = MousePointerId;

            if (!_isHoldingToPurchase || pointerId == _activePointerId)
                return true;
        }

        var pen = Pen.current;
        if (pen != null && pen.tip.isPressed)
        {
            position = pen.position.ReadValue();
            pointerId = MousePointerId;

            if (!_isHoldingToPurchase || pointerId == _activePointerId)
                return true;
        }

        return false;
    }

    private bool IsPointerOverHighlightedTile(Vector2 screenPosition)
    {
        if (purchaseCamera == null)
            purchaseCamera = Camera.main;

        if (purchaseCamera == null)
            return false;

        var ray = purchaseCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hitInfo, purchaseRaycastDistance, purchaseRaycastMask, QueryTriggerInteraction.Ignore))
            return false;

        if (!tileGrid.TryGetNearestTile(hitInfo.point, out var tile, purchaseRaycastDistance))
            return false;

        return tile == _highlightedTile;
    }

    private void CreateHoldIndicator()
    {
        DestroyHoldIndicator();

        if (holdProgressPrefab == null)
            return;

        Transform parent = selectedTileParent != null ? selectedTileParent : (tileGrid != null ? tileGrid.transform : null);
        if (parent == null)
            return;

        Vector3 position = _highlightedTile != null ? _highlightedTile.worldPos + holdProgressOffset : Vector3.zero;
        _holdProgressInstance = Instantiate(holdProgressPrefab, position, Quaternion.identity, parent);
        _holdProgressIndicator = _holdProgressInstance.GetComponentInChildren<HoldProgressIndicator>();
        if (_holdProgressIndicator == null)
            _holdProgressIndicator = _holdProgressInstance.GetComponent<HoldProgressIndicator>();

        if (_holdProgressInstance != null)
            _holdProgressBaseScale = _holdProgressInstance.transform.localScale;
    }

    private void DestroyHoldIndicator()
    {
        if (_holdProgressInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(_holdProgressInstance);
        else
            DestroyImmediate(_holdProgressInstance);

        _holdProgressInstance = null;
        _holdProgressIndicator = null;
    }

    private void UpdateHoldIndicator(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (_holdProgressIndicator != null)
        {
            _holdProgressIndicator.SetProgress(progress);
            return;
        }

        if (_holdProgressInstance != null)
        {
            var targetScale = _holdProgressBaseScale * Mathf.Max(0.0001f, progress);
            _holdProgressInstance.transform.localScale = targetScale;
        }
    }

    private void UpdateHoldIndicatorPosition()
    {
        if (_holdProgressInstance == null || _highlightedTile == null)
            return;

        _holdProgressInstance.transform.position = _highlightedTile.worldPos + holdProgressOffset;
    }
}