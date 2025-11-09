using System;
using UnityEngine;
using Tile = TileGrid.Tile;

public class TileSelectionModel : MonoBehaviour
{
    public event Action<Tile> SelectionChanged;
    public Tile Selected { get; private set; }

    public void SetSelectedTile(Tile tile)
    {
        if (Selected == tile) return;
        Selected = tile;
        SelectionChanged?.Invoke(Selected);
    }

    public void ClearSelectedTile() { if (Selected == null) return; Selected = null; SelectionChanged?.Invoke(null); }
}