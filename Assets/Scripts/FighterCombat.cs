using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FighterCombat : MonoBehaviour
{
    [Header("Contrôle")]
    [SerializeField] private bool controlledByPlayer;

    [Header("Références")]
    [SerializeField] private FighterStats fighterStats;
    [SerializeField] private FighterStats targetStats;
    [SerializeField] private FighterCombat targetCombat;

    [Header("Attaque légère")]
    [SerializeField] private float lightAttackDamage = 20f;
    [SerializeField] private float lightAttackStaminaCost = 10f;

    [Header("Charge")]
    [SerializeField] private float chargeStartupDelay = 0.3f;
    [SerializeField] private float chargeRecoveryPerSecond = 25f;

    [Header("Défense simple")]
    [SerializeField] private float defenseDuration = 0.8f;
    [SerializeField] private float defenseStaminaCost = 10f;

    private bool isActing;
    private bool isDefending;
    private float chargeHoldTime;

    public bool IsDefending => isDefending;

    private void Awake()
    {
        if (fighterStats == null)
            fighterStats = GetComponent<FighterStats>();

        if (targetCombat == null && targetStats != null)
            targetCombat = targetStats.GetComponent<FighterCombat>();
    }

    private void Update()
    {
        if (!controlledByPlayer || Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            LightAttack();

        if (Keyboard.current.dKey.wasPressedThisFrame)
            StartDefense();

        if (Keyboard.current.cKey.isPressed && !isActing)
            HandleCharge();
        else
            StopCharge();
    }

    public void LightAttack()
    {
        if (isActing)
            return;

        if (!fighterStats.SpendStamina(lightAttackStaminaCost))
            return;

        StopCharge();
        StartCoroutine(LightAttackRoutine());
    }

    private IEnumerator LightAttackRoutine()
    {
        isActing = true;

        Vector3 startPosition = transform.position;

        Vector3 attackPosition = Vector3.MoveTowards(
            startPosition,
            targetStats.transform.position,
            1f
        );

        const float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(
                startPosition,
                attackPosition,
                elapsed / duration
            );

            yield return null;
        }

        if (targetCombat == null || !targetCombat.IsDefending)
            targetStats.TakeDamage(lightAttackDamage);

        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(
                attackPosition,
                startPosition,
                elapsed / duration
            );

            yield return null;
        }

        transform.position = startPosition;
        isActing = false;
    }

    public void StartDefense()
    {
        if (isActing)
            return;

        if (!fighterStats.SpendStamina(defenseStaminaCost))
            return;

        StopCharge();
        StartCoroutine(DefenseRoutine());
    }

    private IEnumerator DefenseRoutine()
    {
        isActing = true;
        isDefending = true;

        yield return new WaitForSeconds(defenseDuration);

        isDefending = false;
        isActing = false;
    }

    private void HandleCharge()
    {
        chargeHoldTime += Time.deltaTime;

        if (chargeHoldTime < chargeStartupDelay)
            return;

        fighterStats.RecoverStaminaFromCharge(
            chargeRecoveryPerSecond * Time.deltaTime
        );
    }

    private void StopCharge()
    {
        chargeHoldTime = 0f;
    }

    [ContextMenu("Test Defense")]
    private void TestDefense()
    {
        StartDefense();
    }
}