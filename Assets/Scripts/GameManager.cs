using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private GameObject startingTilePrefab;
    [SerializeField] private Transform startingTileParent;

    [Header("Starting Tile")]
    [SerializeField] private bool placeStartingTileOnStart = true;
    [SerializeField] private float TileYOffset = 0f;

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
        GameObject occupant = null;

        if (startingTilePrefab != null)
        {
            Vector3 spawnPosition = centerTile.worldPos + tileGrid.transform.up * TileYOffset;
            Quaternion rotation = tileGrid.transform.rotation;
            occupant = Instantiate(startingTilePrefab, spawnPosition, rotation, parent);
        }

        tileGrid.MarkOccupied(centerTile, occupant);
    }
}