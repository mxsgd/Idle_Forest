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

            var prefab = tile.placedPrefab != null ? tile.placedPrefab : fallbackAvailableTilePrefab;
            if (prefab == null)
                continue;

            var instance = Instantiate(prefab, tile.worldPos, Quaternion.identity, availableTileParent);
            ConfigureAvailableTile(instance);
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

            if (kvp.Value == null)
                continue;

            if (Application.isPlaying)
                Destroy(kvp.Value);
            else
                DestroyImmediate(kvp.Value);
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
        if (_availableTiles.TryGetValue(tile, out var instance))
        {
            tile.available = false;

            if (instance != null)
            {
                if (Application.isPlaying)
                    Destroy(instance);
                else
                    DestroyImmediate(instance);
            }

            _availableTiles.Remove(tile);
        }
    }

    private void ConfigureAvailableTile(GameObject instance)
    {
        if (instance == null)
            return;

        instance.name = instance.name.Replace("(Clone)", string.Empty).Trim() + " (Available)";

        if (!string.IsNullOrEmpty(availableTag))
            instance.tag = availableTag;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!material.HasProperty("_Color"))
                    continue;

                Color color = material.color;
                color.a = availableAlpha;
                material.color = color;
            }
        }

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
    }
}