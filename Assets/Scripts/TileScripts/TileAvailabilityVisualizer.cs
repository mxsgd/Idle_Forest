using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileAvailabilityVisualizer : MonoBehaviour
{
    [Header("Context")]
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileAvailabilityService availability;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileSelectionModel selection;
    [SerializeField] private TileQueryService query;
    [SerializeField] private TileRuntimeStore runtime;

    [Header("Available Tiles (ghost)")]
    [SerializeField] private Transform availableTileParent;
    [SerializeField, Range(0f, 1f)] private float availableAlpha = 0.35f;
    [SerializeField] private string availableTag = "";

    [Header("Selection Highlight")]
    [SerializeField] private Transform selectedTileParent;
    [SerializeField] private GameObject fallbackSelectedTilePrefab;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 0.5f;
    [SerializeField] private string selectedTag = "";
    [SerializeField] private Vector3 selectedOffset = new Vector3(0f, 1f, 0f);

    [Header("Tile Purchase (hold to buy)")]
    [SerializeField, Min(0f)] private float purchaseHoldDuration = 2f;
    [SerializeField, Min(0f)] private float purchaseCost = 0f;
    [SerializeField] private IdleEconomyManager economy;
    [SerializeField] private Transform purchasedTileParent;
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
        if (!purchaseCamera) purchaseCamera = Camera.main;
        if (!economy) economy = IdleEconomyManager.Instance;

        if (!availableTileParent && grid) availableTileParent = grid.transform;
        if (!selectedTileParent && grid) selectedTileParent = grid.transform;
        if (!purchasedTileParent && grid) purchasedTileParent = grid.transform;

        if (!availability)     availability  = FindAnyObjectByType<TileAvailabilityService>();
        if (!placement)        placement     = FindAnyObjectByType<TilePlacementService>();
        if (!selection)        selection     = FindAnyObjectByType<TileSelectionModel>();
        if (!query)            query         = FindAnyObjectByType<TileQueryService>();
        if (!runtime)          runtime       = FindAnyObjectByType<TileRuntimeStore>();
        if (!grid)             grid          = FindAnyObjectByType<TileGrid>();
    }

    private void OnEnable()
    {
        if (selection != null)
            selection.SelectionChanged += OnSelectionChanged;

        RefreshAvailability();
        UpdateSelectedTileHighlight(selection != null ? selection.Selected : null);
    }

    private void Start()
    {
        RefreshAvailability();
        UpdateSelectedTileHighlight(selection != null ? selection.Selected : null);
    }

    private void OnDisable()
    {
        if (selection != null)
            selection.SelectionChanged -= OnSelectionChanged;

        ClearAvailableTiles();
        ClearSelectedTileHighlight();
        ResetPurchaseHold();
    }

    private void Update()
    {
        UpdatePurchaseHold();
    }

    private void OnSelectionChanged(TileGrid.Tile tile)
    {
        UpdateSelectedTileHighlight(tile);
    }

    private void RefreshAvailability()
    {
        if (availability == null)
        {
            ClearAvailableTiles();
            return;
        }

        var currentlyAvailable = new HashSet<TileGrid.Tile>();
        foreach (var tile in availability.GetAvailable())
        {
            if (tile == null) continue;
            var rt = runtime?.Get(tile);
            if (rt != null && rt.occupied) continue;

            currentlyAvailable.Add(tile);
            if (_availableTiles.ContainsKey(tile)) continue;

            var instance = placement?.PlaceAvailability(tile, availableAlpha, availableTag);
            if (instance != null)
                _availableTiles.Add(tile, instance);
        }

        RemoveStaleAvailableTiles(currentlyAvailable);
    }

    private void ClearAvailableTiles()
    {
        foreach (var kvp in _availableTiles)
            placement?.RemoveAvailability(kvp.Key);

        _availableTiles.Clear();
    }

    private void RemoveStaleAvailableTiles(HashSet<TileGrid.Tile> currentlyAvailable)
    {
        _removalBuffer.Clear();

        foreach (var kvp in _availableTiles)
            if (!currentlyAvailable.Contains(kvp.Key))
                _removalBuffer.Add(kvp.Key);

        foreach (var tile in _removalBuffer)
            RemoveAvailableTile(tile);

        _removalBuffer.Clear();
    }

    private void RemoveAvailableTile(TileGrid.Tile tile)
    {
        if (!_availableTiles.ContainsKey(tile)) return;

        placement?.RemoveAvailability(tile);
        _availableTiles.Remove(tile);
    }

    private void UpdateSelectedTileHighlight(TileGrid.Tile tile)
    {
        if (tile == _highlightedTile) return;

        ResetPurchaseHold();

        if (_highlightedTile != null)
            placement?.RemoveAvailability(_highlightedTile);

        _selectedTileInstance = null;

        if (tile == null) { _highlightedTile = null; return; }

        _selectedTileInstance = placement?.PlaceAvailabilitySelected(tile, selectedAlpha, selectedTag);

        _highlightedTile = tile;
    }
    private void ClearSelectedTileHighlight()
    {
        if (_highlightedTile != null)
            placement?.RemoveAvailability(_highlightedTile);

        _selectedTileInstance = null;
        _highlightedTile = null;
    }

    private static void ConfigureSelectionInstance(GameObject instance, float alpha, string tag)
    {
        if (!instance) return;

        instance.name = instance.name.Replace("(Clone)", "").Trim() + " (Selected)";
        if (!string.IsNullOrEmpty(tag)) instance.tag = tag;

        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m.HasProperty("_Color")) continue;
                var c = m.color; c.a = alpha; m.color = c;
            }
        }
        foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }
    
    private void UpdatePurchaseHold()
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
        {
            ResetPurchaseHold();
            return;
        }

        if (_highlightedTile == null || purchaseHoldDuration <= 0f)
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
            UpdateHoldIndicator(1f);

        ResetPurchaseHold();
    }

    private void BeginPurchaseHold(int pointerId)
    {
        if (_highlightedTile == null) return;

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
        if (tile == null || placement == null || runtime == null)
            return false;

        var rt = runtime.Get(tile);
        if (rt.occupied) return false;
        if (!_availableTiles.ContainsKey(tile)) return false;

        var wallet = economy ?? IdleEconomyManager.Instance;
        if (purchaseCost > 0f)
        {
            if (wallet == null || !wallet.TrySpend(purchaseCost))
                return false;
            economy = wallet;
        }

        var parent = purchasedTileParent ? purchasedTileParent : grid ? grid.transform : null;
        if (!parent)
        {
            if (purchaseCost > 0f && wallet != null) wallet.AddIncome(purchaseCost);
            Debug.LogWarning("[TileAvailabilityVisualizer] Brak rodzica dla kupionego kafelka.", this);
            return false;
        }

        var inst = placement.PlaceOccupant(tile, Quaternion.identity);
        if (inst == null)
        {
            if (purchaseCost > 0f && wallet != null) wallet.AddIncome(purchaseCost);
            Debug.LogWarning("[TileAvailabilityVisualizer] Nie udało się postawić kafelka.", this);
            return false;
        }

        if (purchasedTileOffset != Vector3.zero)
            inst.transform.position += purchasedTileOffset;

        RemoveAvailableTile(tile);
        UpdateSelectedTileHighlight(tile);
        RefreshAvailability();
        return true;
    }

    private bool TryGetActivePointer(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = int.MinValue;

        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var touch in ts.touches)
            {
                if (touch == null || !touch.press.isPressed) continue;
                position = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                if (!_isHoldingToPurchase || pointerId == _activePointerId) return true;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            position = mouse.position.ReadValue();
            pointerId = MousePointerId;
            if (!_isHoldingToPurchase || pointerId == _activePointerId) return true;
        }

        var pen = Pen.current;
        if (pen != null && pen.tip.isPressed)
        {
            position = pen.position.ReadValue();
            pointerId = MousePointerId;
            if (!_isHoldingToPurchase || pointerId == _activePointerId) return true;
        }

        return false;
    }

    private bool IsPointerOverHighlightedTile(Vector2 screenPosition)
    {
        if (!purchaseCamera) purchaseCamera = Camera.main;
        if (!purchaseCamera || query == null) return false;

        var ray = purchaseCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, purchaseRaycastDistance, purchaseRaycastMask, QueryTriggerInteraction.Ignore))
            return false;

        if (!query.TryGetNearestTile(hit.point, out var tile, purchaseRaycastDistance))
            return false;

        return tile == _highlightedTile;
    }

    private void CreateHoldIndicator()
    {
        DestroyHoldIndicator();
        if (!holdProgressPrefab) return;

        var parent = selectedTileParent ? selectedTileParent : grid ? grid.transform : null;
        if (!parent) return;

        var pos = _highlightedTile != null ? _highlightedTile.worldPos + holdProgressOffset : Vector3.zero;
        _holdProgressInstance = Instantiate(holdProgressPrefab, pos, Quaternion.identity, parent);
        _holdProgressIndicator = _holdProgressInstance.GetComponentInChildren<HoldProgressIndicator>();
        if (_holdProgressIndicator == null)
            _holdProgressIndicator = _holdProgressInstance.GetComponent<HoldProgressIndicator>();

        if (_holdProgressInstance) _holdProgressBaseScale = _holdProgressInstance.transform.localScale;
    }


    private void DestroyHoldIndicator()
    {
        if (!_holdProgressInstance) return;
        if (Application.isPlaying) Destroy(_holdProgressInstance); else DestroyImmediate(_holdProgressInstance);
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
        if (!_holdProgressInstance || _highlightedTile == null) return;
        _holdProgressInstance.transform.position = _highlightedTile.worldPos + holdProgressOffset;
    }
}