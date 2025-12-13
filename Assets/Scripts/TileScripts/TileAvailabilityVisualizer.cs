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
    [SerializeField] private TileDeck tileDeck;

    [Header("Available Tiles (ghost)")]
    [SerializeField] private Transform availableTileParent;
    [SerializeField, Range(0f, 1f)] private float availableAlpha = 0.35f;
    [SerializeField] private string availableTag = "";


    [Header("Selection Highlight")]
    [SerializeField] private Transform selectedTileParent;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 0.5f;
    [SerializeField] private string selectedTag = "";


    private readonly Dictionary<TileGrid.Tile, GameObject> _availableTiles = new Dictionary<TileGrid.Tile, GameObject>();
    private readonly List<TileGrid.Tile> _removalBuffer = new List<TileGrid.Tile>();

    private TileGrid.Tile _highlightedTile;
    private GameObject _selectedTileInstance;

    private const int MousePointerId = -1;
    private bool _deckDepleted;

    private void Awake()
    {

        if (!availableTileParent && grid) availableTileParent = grid.transform;
        if (!selectedTileParent && grid) selectedTileParent = grid.transform;

        if (!availability)     availability  = FindAnyObjectByType<TileAvailabilityService>();
        if (!placement)        placement     = FindAnyObjectByType<TilePlacementService>();
        if (!selection)        selection     = FindAnyObjectByType<TileSelectionModel>();
        if (!query)            query         = FindAnyObjectByType<TileQueryService>();
        if (!runtime)          runtime       = FindAnyObjectByType<TileRuntimeStore>();
        if (!grid)             grid          = FindAnyObjectByType<TileGrid>();
        if (!tileDeck)         tileDeck      = FindAnyObjectByType<TileDeck>();
    }

    private void OnEnable()
    {
        if (selection != null)
            selection.SelectionChanged += OnSelectionChanged;

        if (tileDeck != null)
            tileDeck.DeckEmptied += OnDeckEmptied;
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
        if (tileDeck != null)
            tileDeck.DeckEmptied -= OnDeckEmptied;
        ClearAvailableTiles();
        ClearSelectedTileHighlight();
    }

    private void Update()
    {
        TryPlaceHighlightedTile();
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
        if (tile != null)
        {
            var runtimeTile = runtime?.Get(tile);
            if (runtimeTile == null || runtimeTile.occupied || !runtimeTile.available)
            {
                selection?.ClearSelectedTile();
                tile = null;
            }
        }
        if (tile == _highlightedTile) return;

        if (_highlightedTile != null)
        {
            placement?.RemoveAvailability(_highlightedTile);

            if (_availableTiles.ContainsKey(_highlightedTile))
            {
                var ghost = placement.PlaceAvailability(_highlightedTile, availableAlpha, availableTag);
                _availableTiles[_highlightedTile] = ghost;
            }
            else
            {
                _availableTiles.Remove(_highlightedTile);
            }

        }
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

    private void TryPlaceHighlightedTile()
    {
        if (!isActiveAndEnabled || !Application.isPlaying || _deckDepleted)
            return;
        

        if (_highlightedTile == null)
            return;

        if (!TryGetPointerDown(out var position, out var pointerId))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            return;
        if (!IsPointerOverHighlightedTile(position))
            return;

        if (placement == null)
            return;

        var runtimeTile = runtime?.Get(_highlightedTile);
        if (runtimeTile == null || runtimeTile.occupied || !runtimeTile.available)
            return;

        var rotation = grid ? grid.transform.rotation : Quaternion.identity;
        var draw = tileDeck != null ? tileDeck.DrawTile() : null;
        if (tileDeck != null && draw == null)
            return;
            var instance = placement.PlaceOccupant(_highlightedTile, rotation, draw);
        if (instance == null)
            return;

        selection?.ClearSelectedTile();
        RefreshAvailability();
    
    }

    private bool IsPointerOverHighlightedTile(Vector2 screenPosition)
    {
        var camera = Camera.main;
        if (!camera || query == null) return false;
        var ray = camera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            return false;
            
        if (!query.TryGetNearestTile(hit.point, out var tile, 500f))
            return false;

        return tile == _highlightedTile;
    }
    private bool TryGetPointerDown(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = int.MinValue;

        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var touch in ts.touches)
            {
                if (touch == null || !touch.press.wasPressedThisFrame) continue;
                position = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                return true;
            }
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            pointerId = MousePointerId;
            return true;
        }
        var pen = Pen.current;
        if (pen != null && pen.tip.wasPressedThisFrame)
        {
            position = pen.position.ReadValue();
            pointerId = MousePointerId;
            return true;
        }
        return false;
    }
    private void OnDeckEmptied()
    {
        _deckDepleted = true;
        ClearSelectedTileHighlight();
        ClearAvailableTiles();
        enabled = false;
    }
}
