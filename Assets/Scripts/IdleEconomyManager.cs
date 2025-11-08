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

    [Header("Currency")]
    [SerializeField] private float startingCurrency = 0f;
    [SerializeField] private string currencyFormat = "{0:N0}";
    [SerializeField] private TMP_Text currencyText;

    [Header("Income")] 
    [SerializeField] private float incomeTickInterval = 0.5f;

    private readonly List<IIncomeSource> incomeSources = new();
    private float currency;
    private float incomeTimer;

    public event Action<float> CurrencyChanged;

    public float Currency => currency;

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (incomeSources.Count == 0 || incomeTickInterval <= 0f)
            return;

        incomeTimer += Time.deltaTime;
        if (incomeTimer < incomeTickInterval)
            return;

        int ticks = Mathf.FloorToInt(incomeTimer / incomeTickInterval);
        incomeTimer -= ticks * incomeTickInterval;

        float incomePerTick = 0f;
        for (int i = 0; i < incomeSources.Count; i++)
        {
            if (incomeSources[i] != null)
                incomePerTick += Mathf.Max(0f, incomeSources[i].IncomePerTick);
        }

        if (incomePerTick > 0f)
            AddCurrency(incomePerTick * ticks);
    }

    public void RegisterIncomeSource(IIncomeSource source)
    {
        if (source == null || incomeSources.Contains(source))
            return;

        incomeSources.Add(source);
    }

    public void UnregisterIncomeSource(IIncomeSource source)
    {
        if (source == null)
            return;

        incomeSources.Remove(source);
    }

    public void AddIncome(float amount)
    {
        if (amount <= 0f)
            return;

        AddCurrency(amount);
    }

    public bool TrySpend(float cost)
    {
        if (cost <= 0f)
            return true;

        if (currency < cost)
        {
            Debug.Log("Za malo pieniedzy!");
            return false;
        }

        currency -= cost;
        UpdateCurrencyUI();
        CurrencyChanged?.Invoke(currency);
        return true;
    }

    private void AddCurrency(float amount)
    {
        if (amount <= 0f)
            return;

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