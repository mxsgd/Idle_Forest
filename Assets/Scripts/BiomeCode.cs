using System.Collections.Generic;
using UnityEngine;

public enum BiomeCode
{
    Plain = 'x',
    Forest = 'y',
    Bushes = 'z',
    Rocks = 'w',
    Water = 'r'
}

public enum AnimalType
{
    Deer,
    Beaver,
    Bear
}

public static class BiomeCodeUtility
{
    public static readonly Vector2Int[] AxialDirections =
    {
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1)
    };

    private static readonly Dictionary<AnimalType, BiomeCode[]> AnimalPatterns = new()
    {
        { AnimalType.Deer, new[] { BiomeCode.Plain, BiomeCode.Plain, BiomeCode.Forest, BiomeCode.Bushes, BiomeCode.Water } },
        { AnimalType.Beaver, new[] { BiomeCode.Forest, BiomeCode.Forest, BiomeCode.Bushes, BiomeCode.Water, BiomeCode.Water } },
        { AnimalType.Bear, new[] { BiomeCode.Plain, BiomeCode.Forest, BiomeCode.Rocks, BiomeCode.Rocks, BiomeCode.Water } },
    };

    public static BiomeCode ToBiomeCode(this TileType type)
    {
        return type switch
        {
            TileType.Field => BiomeCode.Plain,
            TileType.Bushes => BiomeCode.Bushes,
            TileType.Forest => BiomeCode.Forest,
            TileType.Rocks => BiomeCode.Rocks,
            TileType.Water => BiomeCode.Water,
            _ => BiomeCode.Plain
        };
    }

    public static bool TryGetAnimalPattern(AnimalType animal, out BiomeCode[] pattern)
    {
        return AnimalPatterns.TryGetValue(animal, out pattern);
    }
}