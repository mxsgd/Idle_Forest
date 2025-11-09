using UnityEngine;

/// <summary>
/// Component stored on plant prefabs to keep track of which tile they occupy.
/// </summary>
public class TileObject : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileGrid.Tile tile;

    public TileGrid Grid => grid;
    public TileGrid.Tile Tile => tile;

    public void AssignTile(TileGrid parentGrid, TileGrid.Tile assignedTile)
    {
        grid = parentGrid;
        tile = assignedTile;
    }

}