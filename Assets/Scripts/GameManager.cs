using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private GameObject startingTilePrefab;
    [SerializeField] private Transform startingTileParent;

    [Header("Starting Tile")]
    [SerializeField] private bool placeStartingTileOnStart = true;

    private void Start()
    {
        if (placeStartingTileOnStart)
            PlaceStartingTile();
    }

    public void PlaceStartingTile()
    {
        if (tileGrid == null)
            return;

        TileGrid.Tile centerTile = tileGrid.GetCenterTile();
        if (centerTile == null || centerTile.occupied)
            return;

        Transform parent = startingTileParent != null ? startingTileParent : tileGrid.transform;
        Quaternion rotation = tileGrid.transform.rotation;

        tileGrid.PlaceTile(startingTilePrefab, centerTile, parent, rotation);
    }
}