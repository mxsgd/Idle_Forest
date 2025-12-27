using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tile = TileGrid.Tile;

public class AnimalSequenceService : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtime;

    public bool TryFindValidPath(AnimalType animal, out List<Tile> path)
    {
        path = null;
        if (grid == null || runtime == null)
            return false;

        if (!BiomeCodeUtility.TryGetAnimalPattern(animal, out var pattern) || pattern == null || pattern.Length == 0)
            return false;

        foreach (var start in grid.tiles.OrderBy(t => t.q).ThenBy(t => t.r))
        {
            if (!TileMatchesBiome(start, pattern[0]))
                continue;

            var visited = new HashSet<Tile>();
            if (TryExtend(start, pattern, 0, visited, out var found))
            {
                path = found;
                return true;
            }
        }

        return false;
    }

    private bool TileMatchesBiome(Tile tile, BiomeCode expected)
    {
        if (tile == null)
            return false;

        var runtimeState = runtime.Get(tile);
        var biome = runtimeState?.biomeCode ?? tile.biome;
        return biome == expected;
    }

    private bool TryExtend(Tile current, IReadOnlyList<BiomeCode> pattern, int index, HashSet<Tile> visited, out List<Tile> path)
    {
        visited.Add(current);
        if (index == pattern.Count - 1)
        {
            path = new List<Tile> { current };
            visited.Remove(current);
            return true;
        }

        for (int dir = 0; dir < current.neighbors.Length; dir++)
        {
            var neighbor = current.neighbors[dir];
            if (neighbor == null || visited.Contains(neighbor))
                continue;

            if (!TileMatchesBiome(neighbor, pattern[index + 1]))
                continue;

            if (TryExtend(neighbor, pattern, index + 1, visited, out var tail))
            {
                tail.Insert(0, current);
                visited.Remove(current);
                path = tail;
                return true;
            }
        }

        visited.Remove(current);
        path = null;
        return false;
    }
}