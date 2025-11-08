using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TileClickSelector : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private TileGrid grid;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float maxRayDistance = 500f;

    private TileGrid.Tile _selectedTile;

    public TileGrid.Tile SelectedTile => _selectedTile;

    public event Action<TileGrid.Tile> TileSelected;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (grid == null)
            grid = GetComponent<TileGrid>();
    }

    private void OnDisable()
    {
        ClearSelectionInternal(true);
    }

    private void Update()
    {
        if (grid == null || mainCamera == null)
            return;

        if (!TryGetPointerDown(out var screenPosition, out var pointerId))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            return;

        if (RaycastToWorld(screenPosition, out var worldPoint) && grid.TryGetNearestTile(worldPoint, out var tile, maxRayDistance))
        {
            SetSelection(tile);
        }
        else
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        ClearSelectionInternal(true);
    }

    private void SetSelection(TileGrid.Tile tile)
    {
        if (tile == null || tile == _selectedTile)
            return;

        _selectedTile = tile;
        if (grid != null)
            grid.SetSelectedTile(tile);

        TileSelected?.Invoke(tile);
    }

    private void ClearSelectionInternal(bool notifyGrid)
    {
        if (_selectedTile == null)
            return;

        _selectedTile = null;

        if (notifyGrid && grid != null)
            grid.ClearSelectedTile();

        TileSelected?.Invoke(null);
    }

    private bool TryGetPointerDown(out Vector2 position, out int pointerId)
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    position = touch.position;
                    pointerId = touch.fingerId;
                    return true;
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pointerId = -1;
            return true;
        }

        position = default;
        pointerId = -1;
        return false;
    }

    private bool RaycastToWorld(Vector2 screenPos, out Vector3 worldPoint)
    {
        var ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out var hitInfo, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            worldPoint = hitInfo.point;
            return true;
        }

        worldPoint = default;
        return false;
    }
}