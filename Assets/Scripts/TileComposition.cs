using System;
using UnityEngine;

[Serializable]
public class TileComposition
{
    private static readonly float[] GrassProductionByLevel = { 0f, 1f, 2f, 3f };
    private static readonly float[] BushProductionByLevel = { 0f, 3f, 5f, 8f };
    private static readonly float[] TreeProductionByLevel = { 0f, 8f, 12f, 18f };

    private const float BerriesContribution = 2f;

    [Header("Grass")]
    [Range(0, 3)]
    public int levelGrass = 0;

    [Header("Bush")]
    public bool hasBush = false;
    [Range(1, 3)]
    public int bushLevel = 1;

    [Header("Berries")]
    public bool hasBerries = false;

    [Header("Tree")]
    public bool hasTree = false;
    [Range(1, 3)]
    public int treeLevel = 1;

    public TileDensity GetDensity(float lowThreshold = 5f, float highThreshold = 12f)
    {
        float production = GetProduction();

        if (production < lowThreshold)
            return TileDensity.Low;
        if (production < highThreshold)
            return TileDensity.Medium;
        return TileDensity.High;
    }

    public TileElement GetDominantElement()
    {
        float bestValue = GetElementContribution(TileElement.Grass);
        TileElement bestElement = TileElement.Grass;

        foreach (TileElement element in Enum.GetValues(typeof(TileElement)))
        {
            float contribution = GetElementContribution(element);
            if (contribution > bestValue)
            {
                bestValue = contribution;
                bestElement = element;
            }
        }

        return bestElement;
    }

    public float GetProduction()
    {
        float total = GetElementContribution(TileElement.Grass);
        total += GetElementContribution(TileElement.Bush);
        total += GetElementContribution(TileElement.Tree);
        total += GetElementContribution(TileElement.Berries);
        return total;
    }

    public float GetElementContribution(TileElement element)
    {
        switch (element)
        {
            case TileElement.Grass:
                return GrassProductionByLevel[Mathf.Clamp(levelGrass, 0, 3)];
            case TileElement.Bush:
                return hasBush ? BushProductionByLevel[Mathf.Clamp(bushLevel, 1, 3)] : 0f;
            case TileElement.Tree:
                return hasTree ? TreeProductionByLevel[Mathf.Clamp(treeLevel, 1, 3)] : 0f;
            case TileElement.Berries:
                return hasBerries ? BerriesContribution : 0f;
            default:
                return 0f;
        }
    }

    public void Validate()
    {
        levelGrass = Mathf.Clamp(levelGrass, 0, 3);

        if (!hasBush)
        {
            bushLevel = 1;
        }
        else
        {
            bushLevel = Mathf.Clamp(bushLevel, 1, 3);
        }

        if (!hasTree)
        {
            treeLevel = 1;
        }
        else
        {
            treeLevel = Mathf.Clamp(treeLevel, 1, 3);
        }
    }
}

public enum TileDensity
{
    Low,
    Medium,
    High
}

public enum TileElement
{
    Grass,
    Bush,
    Berries,
    Tree
}