using UnityEngine;
using System;
using Tile = TileGrid.Tile;
public static class TileEvents
{
    public static event Action<Tile, TileBuildAction> CompositionChanged;
    public static event Action<Tile> TileStateChanged;

    public static void RaiseCompositionChanged(Tile tile, TileBuildAction action)
    => CompositionChanged?.Invoke(tile, action);

    public static void RaiseTileStateChanged(Tile tile)
    => TileStateChanged?.Invoke(tile);

}
