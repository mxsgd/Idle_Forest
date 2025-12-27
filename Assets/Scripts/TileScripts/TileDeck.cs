using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TileType
{
    Field,
    Bushes,
    Forest,
    Rocks,
    Water
}

[Serializable]
public class TilePrefabGroup
{
    [Tooltip("Rodzaj kafla: pole, krzaki, las lub woda.")]
    public TileType tileType = TileType.Field;

    [Tooltip("Prefaby przypisane do tego rodzaju kafla.")]
    public List<GameObject> prefabs = new();

    [Tooltip("Ikona dla UI podglądu talii.")]
    public Sprite icon;

    [Tooltip("Etykieta używana w UI. Domyślnie z nazwy typu kafla.")]
    public string displayName;
}

[Serializable]
public class TileDraw
{
    public TileType tileType;
    public GameObject prefab;
    public Sprite icon;
    public string displayName;

    public TileDraw(TileType type, GameObject prefab, Sprite icon, string displayName)
    {
        tileType = type;
        this.prefab = prefab;
        this.icon = icon;
        this.displayName = displayName;
    }
}
public class TileDeck : MonoBehaviour
{
    [Header("Ustawienia talii")]
    [SerializeField, Min(1)] private int deckSize = 30;
    [SerializeField] private bool rebuildOnStart = true;

    [Header("Pula kafli do losowania")]
    [SerializeField] private List<TilePrefabGroup> tileGroups = new();
    private readonly Queue<TileDraw> _deck = new();
    public event Action<IReadOnlyList<TileDraw>> DeckChanged;
    public event Action DeckEmptied;
    public TileDraw Current => _deck.Count > 0 ? _deck.Peek() : null;
    public bool IsEmpty => _deck.Count == 0;

    private void Awake()
    {
        if (rebuildOnStart)
            RebuildDeck();
    }

    public void RebuildDeck()
    {
        _deck.Clear();
        var pool = BuildPool();
        if (pool.Count == 0)
        {
            NotifyDeckChanged();
            return;
        }

        var buffer = new List<TileDraw>(pool.Count);
        while (_deck.Count < deckSize)
        {
            buffer.Clear();
            buffer.AddRange(pool);
            Shuffle(buffer);
            foreach (var draw in buffer)
            {
                _deck.Enqueue(draw);
                if (_deck.Count >= deckSize)
                    break;
            }
        }
        NotifyDeckChanged();
    }

    public TileDraw DrawTile()
    {
        if (_deck.Count == 0)
        {
            DeckEmptied?.Invoke();
            return null;
        }

        var draw = _deck.Dequeue();
        NotifyDeckChanged();

        if (_deck.Count == 0)
            DeckEmptied?.Invoke();
        
        return draw;
    }
    
    public static string GetTypeLabel(TileType type)
    {
        return type switch
        {
            TileType.Field => "Pole",
            TileType.Bushes => "Krzaki",
            TileType.Rocks => "Skały",
            TileType.Forest => "Las",
            TileType.Water => "Woda",
            _ => type.ToString()
        };
    }

    private List<TileDraw> BuildPool()
    {
        var pool = new List<TileDraw>();

        foreach (var group in tileGroups)
        {
            if (group == null || group.prefabs == null)
                continue;

            var label = string.IsNullOrWhiteSpace(group.displayName)
                ? GetTypeLabel(group.tileType)
                : group.displayName;

            foreach (var prefab in group.prefabs)
            {
                if (prefab == null) continue;
                pool.Add(new TileDraw(group.tileType, prefab, group.icon, label));
            }
        }

        return pool;
    }

    public IReadOnlyList<TileDraw> GetQueuedTiles()
    {
        return _deck.ToList();
    }

    private void NotifyDeckChanged()
    {
        DeckChanged?.Invoke(GetQueuedTiles());
    }

        private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
