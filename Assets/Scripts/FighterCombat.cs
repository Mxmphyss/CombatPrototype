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

    [Header("Garde maintenue")]
    [SerializeField] private float heldGuardStaminaCostPerSecond = 15f;

    [Header("Esquive")]
    [SerializeField] private float dodgeStaminaCost = 20f;
    [SerializeField] private float dodgeDistance = 1.25f;
    [SerializeField] private float dodgeDuration = 0.18f;

    private bool isActing;
    private bool isDefending;
    private bool isHoldingGuard;
    private float chargeHoldTime;

    public bool IsDefending => isDefending;
    public bool IsPlayerControlled => controlledByPlayer;

    private void Awake()
    {
        if (fighterStats == null)
            fighterStats = GetComponent<FighterStats>();

        if (targetCombat == null && targetStats != null)
            targetCombat = targetStats.GetComponent<FighterCombat>();
    }

    private void Update()
    {
        UpdateHeldGuard();

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

    public bool StartHeldGuard()
    {
        if (isActing || isHoldingGuard)
            return false;

        if (!fighterStats.SpendStamina(
                heldGuardStaminaCostPerSecond * Time.deltaTime))
            return false;

        StopCharge();
        isHoldingGuard = true;
        isDefending = true;
        return true;
    }

    public void StopHeldGuard()
    {
        if (!isHoldingGuard)
            return;

        isHoldingGuard = false;
        isDefending = false;
    }

    public void DodgeLeft()
    {
        StartDodge(-1f);
    }

    public void DodgeRight()
    {
        StartDodge(1f);
    }

    private IEnumerator DefenseRoutine()
    {
        isActing = true;
        isDefending = true;

        yield return new WaitForSeconds(defenseDuration);

        isDefending = false;
        isActing = false;
    }

    private void UpdateHeldGuard()
    {
        if (!isHoldingGuard)
            return;

        float cost = heldGuardStaminaCostPerSecond * Time.deltaTime;
        if (!fighterStats.SpendStamina(cost))
            StopHeldGuard();
    }

    private void StartDodge(float direction)
    {
        if (isActing || isHoldingGuard)
            return;

        if (!fighterStats.SpendStamina(dodgeStaminaCost))
            return;

        StopCharge();
        StartCoroutine(DodgeRoutine(direction));
    }

    private IEnumerator DodgeRoutine(float direction)
    {
        isActing = true;

        Vector3 startPosition = transform.position;
        Vector3 sideDirection = transform.right * direction;
        Vector3 dodgePosition = startPosition + sideDirection * dodgeDistance;

        float halfDuration = dodgeDuration * 0.5f;
        yield return MoveBetween(startPosition, dodgePosition, halfDuration);
        yield return MoveBetween(dodgePosition, startPosition, halfDuration);

        transform.position = startPosition;
        isActing = false;
    }

    private IEnumerator MoveBetween(
        Vector3 from,
        Vector3 to,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(
                from,
                to,
                Mathf.Clamp01(elapsed / duration)
            );
            yield return null;
        }
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
