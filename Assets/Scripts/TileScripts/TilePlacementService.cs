using UnityEngine;
using System;
using Tile = TileGrid.Tile;

public class TilePlacementService : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private Transform occupantsParent;
    [SerializeField] private Transform availabilityParent;
    [SerializeField] private GameObject availabilityPrefab;
    [SerializeField] private GameObject availabilitySelectedTilePrefab;
    [SerializeField] private GameObject occupantPrefab;
    [SerializeField] private Vector3 occupantOffset = new(0, 1f, 0);
    [SerializeField] private Vector3 availabilityOffset = new(0, 1f, 0);

    public event Action<Tile> TileStateChanged;

    public GameObject PlaceOccupant(Tile tile, Quaternion rotation)
    {
        if (tile == null || occupantPrefab == null) return null;
        var r = runtimeStore.Get(tile);
        if (r.occupied) return null;

        RemoveAvailability(tile); // jak u Ciebie
        var pos = tile.worldPos + occupantOffset;
        var go = Instantiate(occupantPrefab, pos, rotation, occupantsParent);
        runtimeStore.MarkOccupied(tile, go, occupantPrefab);
        return go;
    }

    public GameObject PlaceAvailability(Tile tile, float alpha, string tag)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        var tpl = availabilityPrefab != null ? availabilityPrefab : r.templatePrefab;
        if (tpl == null) return null;

        RemoveAvailability(tile);
        var pos = tile.worldPos + availabilityOffset;
        var go = Instantiate(tpl, pos, Quaternion.identity, availabilityParent);
        ConfigureAvailabilityInstance(go, alpha, tag);
        r.availabilityInstance = go;
        r.available = true;
        return go;
    }

    public GameObject PlaceAvailabilitySelected(Tile tile, float alpha, string tag)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        var tpl = availabilitySelectedTilePrefab != null ? availabilitySelectedTilePrefab : r.templatePrefab;
        if (tpl == null) return null;

        RemoveAvailability(tile);
        var pos = tile.worldPos + availabilityOffset;
        var go = Instantiate(tpl, pos, Quaternion.identity, availabilityParent);
        ConfigureAvailabilityInstance(go, alpha, tag);
        r.availabilityInstance = go;
        r.available = true;
        return go;
    }

    public void RemoveAvailability(Tile tile)
    {
        var r = runtimeStore.Get(tile);
        if (r?.availabilityInstance == null) return;
        Destroy(r.availabilityInstance);
        r.availabilityInstance = null;
        r.available = false;
    }

    public void RemoveOccupant(Tile tile)
    {
        var r = runtimeStore.Get(tile);
        if (r?.occupantInstance == null) return;
        Destroy(r.occupantInstance);
        r.occupantInstance = null;
        runtimeStore.Free(tile);
    }

    private static void ConfigureAvailabilityInstance(GameObject instance, float alpha, string tag)
    {
        if (instance == null) return;
        instance.name = instance.name.Replace("(Clone)", "").Trim() + " (Available)";
        if (!string.IsNullOrEmpty(tag)) instance.tag = tag;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m.HasProperty("_Color")) continue;
                var c = m.color; c.a = alpha; m.color = c;
            }
        }
        foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }
}