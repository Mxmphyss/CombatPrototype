using UnityEngine;

public class FighterStats : MonoBehaviour
{
    [Header("Points de vie")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Endurance")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;

    [SerializeField] private bool isStaminaCritical;

    public bool IsStaminaCritical => isStaminaCritical;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        isStaminaCritical = false;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
    }

    public bool SpendStamina(float amount)
    {
        if (currentStamina < amount)
            return false;

        currentStamina -= amount;

        if (currentStamina <= 0.1f)
        {
            currentStamina = 0f;
            isStaminaCritical = true;
        }

        return true;
    }

    public void RecoverStaminaFromCharge(float amount)
    {
        currentStamina += amount;
        currentStamina = Mathf.Min(currentStamina, maxStamina);

        if (currentStamina > 0f)
            isStaminaCritical = false;
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
