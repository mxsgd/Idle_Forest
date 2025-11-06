using UnityEngine;
using UnityEngine.InputSystem;

public class PlantPlacer : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TileGrid grid;
    public GameObject plantPrefab;
    public Transform parentForPlants;
    public LayerMask groundMask = ~0;

    [Header("Placement")]
    public float maxSnapDistance = 999f;
    public float yOffset = 0.0f;
    public Vector3 placementCenter = new Vector3(0f, 0f, 0f);

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (parentForPlants == null && grid != null)
            parentForPlants = grid.transform;
    }

    public void TryPlacePlant(PlantDefinition plant)
    {
            if (grid.TryGetNearestFreeTile(placementCenter, out var tile, maxSnapDistance))
            {
                var go = Instantiate(plant.prefab, tile.worldPos + Vector3.up * yOffset, Quaternion.identity, parentForPlants);

            Quaternion baseRot = Quaternion.identity;
            go.transform.rotation = baseRot;
                
                grid.MarkOccupied(tile, go);
            }
    }
}
