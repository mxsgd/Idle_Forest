using System.Collections.Generic;
using UnityEngine;

public class TileAvailabilityVisualizer : MonoBehaviour
{
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private Transform availableTileParent;
    [SerializeField] private GameObject fallbackAvailableTilePrefab;
    [SerializeField, Range(0f, 1f)] private float availableAlpha = 0.35f;
    [SerializeField] private string availableTag = "";

    private readonly Dictionary<TileGrid.Tile, GameObject> _availableTiles = new Dictionary<TileGrid.Tile, GameObject>();
    private readonly List<TileGrid.Tile> _removalBuffer = new List<TileGrid.Tile>();

    private void Awake()
    {
        if (tileGrid == null)
            tileGrid = GetComponent<TileGrid>();

        if (availableTileParent == null && tileGrid != null)
            availableTileParent = tileGrid.transform;
    }

    private void OnEnable()
    {
        if (tileGrid != null)
        {
            tileGrid.TileStateChanged += HandleTileStateChanged;
        }

        RefreshAvailability();
    }

    private void Start()
    {
        RefreshAvailability();
    }

    private void OnDisable()
    {
        if (tileGrid != null)
        {
            tileGrid.TileStateChanged -= HandleTileStateChanged;
        }

        ClearAvailableTiles();
    }

    private void HandleTileStateChanged(TileGrid.Tile _)
    {
        RefreshAvailability();
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


}