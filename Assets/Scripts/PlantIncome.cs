using UnityEngine;

public class PlantIncome : MonoBehaviour, IIncomeSource
{
    [SerializeField] private PlantDefinition definition;
    [SerializeField] private float incomePerTick;

    private bool isRegistered;

    public float IncomePerTick => incomePerTick;

    private void Awake()
    {
        if (definition != null)
            incomePerTick = Mathf.Max(0f, definition.incomePerTick);
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        Unregister();
    }

    public void Initialize(PlantDefinition def)
    {
        definition = def;
        incomePerTick = def != null ? Mathf.Max(0f, def.incomePerTick) : 0f;

        if (isActiveAndEnabled)
            Register();
    }

    private void Register()
    {
        if (isRegistered)
            return;

        var economy = IdleEconomyManager.Instance;
        if (economy == null)
            return;

        economy.RegisterIncomeSource(this);
        isRegistered = true;
    }

    private void Unregister()
    {
        if (!isRegistered)
            return;

        var economy = IdleEconomyManager.Instance;
        if (economy != null)
            economy.UnregisterIncomeSource(this);

        isRegistered = false;
    }
}