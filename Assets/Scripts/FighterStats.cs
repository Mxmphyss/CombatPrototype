using System;
using UnityEngine;

public class FighterStats : MonoBehaviour
{
    [Header("Points de vie")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Endurance")]
    [Min(1f)]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;

    [SerializeField] private bool isStaminaCritical;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<FighterStats> OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsDead => currentHealth <= 0f;
    public bool IsStaminaCritical => isStaminaCritical;

    private bool deathRaised;

    private void Awake()
    {
        ResetStats();
    }

    private void Start()
    {
        NotifyAllValues();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        maxStamina = Mathf.Max(1f, maxStamina);

        if (!Application.isPlaying)
            return;

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        NotifyAllValues();
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead)
            return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(
            currentHealth - damage,
            0f,
            maxHealth
        );

        if (!Mathf.Approximately(previousHealth, currentHealth))
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (IsDead && !deathRaised)
        {
            deathRaised = true;
            OnDeath?.Invoke(this);
        }
    }

    public bool SpendStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        if (IsDead || currentStamina + Mathf.Epsilon < amount)
            return false;

        currentStamina = Mathf.Clamp(
            currentStamina - amount,
            0f,
            maxStamina
        );
        isStaminaCritical = currentStamina <= 0.1f;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        return true;
    }

    public void RecoverStamina(float amount)
    {
        if (amount <= 0f || IsDead || currentStamina >= maxStamina)
            return;

        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(
            currentStamina + amount,
            0f,
            maxStamina
        );
        isStaminaCritical = currentStamina <= 0.1f;

        if (!Mathf.Approximately(previousStamina, currentStamina))
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    public void RecoverStaminaFromCharge(float amount)
    {
        RecoverStamina(amount);
    }

    public void ResetStats()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        maxStamina = Mathf.Max(1f, maxStamina);
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        isStaminaCritical = false;
        deathRaised = false;
        NotifyAllValues();
    }

    public void NotifyAllValues()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    [ContextMenu("Test Damage - 20")]
    private void TestDamage()
    {
        TakeDamage(20f);
    }

    [ContextMenu("Test Stamina - 20")]
    private void TestStamina()
    {
        SpendStamina(20f);
    }
}
