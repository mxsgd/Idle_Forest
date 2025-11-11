using UnityEngine;
using System;

[Serializable]
public class TileComposition
{
    private static readonly float[] GrassIncomeByLevel = { 0f, 1f, 2f, 3f };
    private static readonly float[] BushIncomeByLevel  = { 0f, 3f, 5f, 8f };
    private static readonly float[] TreeIncomeByLevel  = { 0f, 8f, 12f, 18f };

    [Header("Grass")]
    [Range(0, 3)] public int levelGrass = 0;

    [Header("Bush")]
    [Range(0, 3)] public int bushLevel = 0;

    [Header("Tree")]
    [Range(0, 3)] public int treeLevel = 0;

    public void Validate()
    {
        levelGrass = Mathf.Clamp(levelGrass, 0, 3);
        bushLevel = Mathf.Clamp(bushLevel, 0, 3);
        treeLevel = Mathf.Clamp(treeLevel, 0, 3);
    }

    public float GetSumIncome()
    {
        float total = 0f;
        total += GrassIncomeByLevel[levelGrass];
        total += BushIncomeByLevel[bushLevel];
        total += TreeIncomeByLevel[treeLevel];


        return total;
    }
    public TileElement GetDominantElement()
    {
        float grass = GrassIncomeByLevel[levelGrass];
        float bush  = BushIncomeByLevel[bushLevel];
        float tree  = TreeIncomeByLevel[treeLevel];

        float max = grass;
        TileElement dominant = TileElement.Grass;

        if (bush > max) { max = bush; dominant = TileElement.Bush; }
        if (tree > max) { max = tree; dominant = TileElement.Tree; }

        return dominant;
    }
}
public enum TileElement
{
    Grass,
    Bush,
    Tree
}