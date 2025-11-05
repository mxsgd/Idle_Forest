using UnityEngine;
using UnityEngine.InputSystem;

public class PlantPlacer : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;             // użyj istniejącej kamery z sceny
    public TileGrid grid;                 // przypnij swój TileGrid (Plane)
    public GameObject plantPrefab;        // prefab rośliny
    public Transform parentForPlants;     // opcjonalnie — gdzie trzymać instancje
    public LayerMask groundMask = ~0;     // warstwa, na której można sadzić

    [Header("Placement")]
    public float maxSnapDistance = 999f;
    public float yOffset = 0.0f;
    public bool alignToGroundNormal = true;

    private void Awake()
    {
        // jeśli nie ustawiono kamery ręcznie — weź główną z tagiem MainCamera
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (parentForPlants == null && grid != null)
            parentForPlants = grid.transform;
    }

    private void Update()
    {
        // obsługa kliknięcia myszką
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceAtScreenPos(Mouse.current.position.ReadValue());
        }

        // obsługa dotyku
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TryPlaceAtScreenPos(Touchscreen.current.primaryTouch.position.ReadValue());
        }
    }

    private void TryPlaceAtScreenPos(Vector2 screenPos)
    {
        if (mainCamera == null || grid == null || plantPrefab == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (grid.TryGetNearestFreeTile(hit.point, out var tile, maxSnapDistance))
            {
                var go = Instantiate(plantPrefab, tile.worldPos + Vector3.up * yOffset, Quaternion.identity, parentForPlants);

                // Ustaw rotację bazową
                Quaternion baseRot = Quaternion.identity;

                if (alignToGroundNormal)
                    baseRot = Quaternion.FromToRotation(Vector3.up, hit.normal);

                // Dodaj stały obrót o 90 stopni na osi X
                Quaternion xRot = Quaternion.Euler(0f, 0f, 0f);

                go.transform.rotation = baseRot * xRot;
                grid.MarkOccupied(tile, go);
            }
        }
    }
}