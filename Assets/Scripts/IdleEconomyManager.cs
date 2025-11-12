using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public interface IIncomeSource
{
    float IncomePerTick { get; }
}

public class IdleEconomyManager : MonoBehaviour
{
    public static IdleEconomyManager Instance { get; private set; }
    public static event Action<IdleEconomyManager> InstanceChanged;

    [Header("Currency")]
    [SerializeField] private float startingCurrency = 0f;
    [SerializeField] private string currencyFormat = "{0:N0}";
    [SerializeField] private TMP_Text currencyText;

    [Header("Income")]
    [SerializeField, Min(0.01f)] private float incomeTickInterval = 0.5f;
    [Tooltip("Jeśli false – dochód nie będzie naliczany.")]
    [SerializeField] private bool incomeEnabled = true;

    [Header("Expansion Cost")]
    [SerializeField, Min(0f)] private float tileCost = 1f;
    [SerializeField, Min(1f)] private float tileCostMultiplier = 2f;

    // === Runtime ===
    private readonly List<IIncomeSource> incomeSources = new();
    private float currency;
    private float incomeTimer;

    // === Events ===
    public event Action<float> CurrencyChanged;
    public event Action<float, int> IncomeTicked;
    public event Action<float> TileCostChanged;

    public float Currency => currency;
    public float IncomeTickInterval
    {
        get => incomeTickInterval;
        set => incomeTickInterval = Mathf.Max(0.01f, value);
    }
    public bool IncomeEnabled
    {
        get => incomeEnabled;
        set => incomeEnabled = value;
    }
    public float TileCost => tileCost;
    public float TileCostMultiplier => tileCostMultiplier;

    public float TotalIncomePerTick
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < incomeSources.Count; i++)
            {
                var s = incomeSources[i];
                if (s != null) sum += Mathf.Max(0f, s.IncomePerTick);
            }
            return sum;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currency = Mathf.Max(0f, startingCurrency);
        UpdateCurrencyUI();

        InstanceChanged?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }

    private void Update()
    {
        if (!incomeEnabled || incomeSources.Count == 0 || incomeTickInterval <= 0f)
            return;

        incomeTimer += Time.deltaTime;
        if (incomeTimer < incomeTickInterval)
            return;

        int ticks = Mathf.FloorToInt(incomeTimer / incomeTickInterval);
        incomeTimer -= ticks * incomeTickInterval;

        float perTick = TotalIncomePerTick;
        if (perTick <= 0f) return;

        float gain = perTick * ticks;
        AddCurrencyInternal(gain);

        IncomeTicked?.Invoke(perTick, ticks);
    }

    // === Registration ===
    public bool RegisterIncomeSource(IIncomeSource source)
    {
        if (source == null) return false;
        if (incomeSources.Contains(source)) return false;
        incomeSources.Add(source);
        return true;
    }

    public bool UnregisterIncomeSource(IIncomeSource source)
    {
        if (source == null) return false;
        return incomeSources.Remove(source);
    }

    // === Currency ops ===
    public void SetCurrency(float value)
    {
        currency = Mathf.Max(0f, value);
        UpdateCurrencyUI();
        CurrencyChanged?.Invoke(currency);
    }

    public void AddIncomeInstant(float amount)
    {
        if (amount <= 0f) return;
        AddCurrencyInternal(amount);
    }

    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (currency < cost) return false;

        currency -= cost;
        UpdateCurrencyUI();
        CurrencyChanged?.Invoke(currency);
        return true;
    }

    public bool TryBuyNextTile()
    {
        if (currency < tileCost) return false;

        currency -= tileCost;
        tileCost *= tileCostMultiplier;

        UpdateCurrencyUI();
        CurrencyChanged?.Invoke(currency);
        TileCostChanged?.Invoke(tileCost);
        return true;
    }

    // === Helpers ===
    private void AddCurrencyInternal(float amount)
    {
        if (amount <= 0f) return;
        currency += amount;
        UpdateCurrencyUI();
        CurrencyChanged?.Invoke(currency);
    }

    private void UpdateCurrencyUI()
    {
        if (currencyText != null)
            currencyText.text = string.Format(currencyFormat, currency);
    }
}