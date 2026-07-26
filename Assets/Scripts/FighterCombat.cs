using System;
using System.Collections;
using UnityEngine;

public enum CombatActionResult
{
    Started,
    Busy,
    NotEnoughStamina,
    Unavailable
}

public enum FighterCombatState
{
    Idle,
    Attacking,
    Defending,
    HoldingGuard,
    Charging,
    Dodging,
    Dead
}

public enum CombatHitResult
{
    Hit,
    Blocked,
    Dodged
}

public class FighterCombat : MonoBehaviour
{
    [Header("Controle")]
    [SerializeField] private bool controlledByPlayer;

    [Header("References")]
    [SerializeField] private FighterStats fighterStats;
    [SerializeField] private FighterStats targetStats;
    [SerializeField] private FighterCombat targetCombat;

    [Header("Attaque legere")]
    [SerializeField] private float lightAttackDamage = 20f;
    [SerializeField] private float lightAttackStaminaCost = 10f;

    [Header("Charge")]
    [SerializeField] private float chargeStartupDelay = 0.3f;
    [SerializeField] private float chargeRecoveryPerSecond = 25f;

    [Header("Defense simple")]
    [SerializeField] private float defenseDuration = 0.8f;
    [SerializeField] private float defenseStaminaCost = 10f;

    [Header("Garde maintenue")]
    [SerializeField] private float heldGuardStaminaCostPerSecond = 15f;

    [Header("Esquive")]
    [SerializeField] private float dodgeStaminaCost = 20f;
    [SerializeField] private float dodgeDistance = 1.25f;
    [SerializeField] private float dodgeDuration = 0.18f;

    public event Action<FighterCombat, FighterCombatState> OnStateChanged;
    public event Action<FighterCombat, CombatHitResult> OnAttackResolved;

    public FighterStats Stats => fighterStats;
    public bool IsDefending => isDefending;
    public bool IsDodging => isDodging;
    public bool IsCharging => isCharging;
    public bool IsBusy => isActing || isHoldingGuard || isCharging;
    public bool IsDead => fighterStats == null || fighterStats.IsDead;
    public bool IsPlayerControlled => controlledByPlayer;
    public float LightAttackStaminaCost => lightAttackStaminaCost;
    public FighterCombatState CurrentState { get; private set; }

    private bool combatEnabled = true;
    private bool isActing;
    private bool isDefending;
    private bool isDodging;
    private bool isHoldingGuard;
    private bool isCharging;
    private float chargeHoldTime;
    private Vector3 restPosition;

    private void Awake()
    {
        if (fighterStats == null)
            fighterStats = GetComponent<FighterStats>();

        if (targetCombat == null && targetStats != null)
            targetCombat = targetStats.GetComponent<FighterCombat>();

        restPosition = transform.position;
        CurrentState = FighterCombatState.Idle;
    }

    private void OnEnable()
    {
        if (fighterStats != null)
            fighterStats.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (fighterStats != null)
            fighterStats.OnDeath -= HandleDeath;

        CancelActiveActions(false);
    }

    private void Update()
    {
        UpdateHeldGuard();
        UpdateCharge();
    }

    public CombatActionResult LightAttack()
    {
        if (!CanStartAction() ||
            targetStats == null ||
            targetStats.IsDead)
        {
            return CombatActionResult.Unavailable;
        }

        if (isActing || isHoldingGuard || isCharging)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(lightAttackStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        StartCoroutine(LightAttackRoutine());
        return CombatActionResult.Started;
    }

    public CombatActionResult StartDefense()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (isActing || isHoldingGuard || isCharging)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(defenseStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        StartCoroutine(DefenseRoutine());
        return CombatActionResult.Started;
    }

    public CombatActionResult StartHeldGuard()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (isActing || isHoldingGuard || isCharging)
            return CombatActionResult.Busy;

        float initialCost =
            heldGuardStaminaCostPerSecond * Time.unscaledDeltaTime;
        if (!fighterStats.SpendStamina(initialCost))
            return CombatActionResult.NotEnoughStamina;

        isHoldingGuard = true;
        isDefending = true;
        SetState(FighterCombatState.HoldingGuard);
        return CombatActionResult.Started;
    }

    public void StopHeldGuard()
    {
        if (!isHoldingGuard)
            return;

        isHoldingGuard = false;
        isDefending = false;
        SetIdleIfAvailable();
    }

    public CombatActionResult StartCharge()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (isActing || isHoldingGuard)
            return CombatActionResult.Busy;

        if (isCharging)
            return CombatActionResult.Started;

        isCharging = true;
        chargeHoldTime = 0f;
        SetState(FighterCombatState.Charging);
        return CombatActionResult.Started;
    }

    public void StopChargeInput()
    {
        if (!isCharging)
            return;

        isCharging = false;
        chargeHoldTime = 0f;
        SetIdleIfAvailable();
    }

    public CombatActionResult DodgeLeft()
    {
        return StartDodge(-1f);
    }

    public CombatActionResult DodgeRight()
    {
        return StartDodge(1f);
    }

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;
        if (!enabled)
            CancelActiveActions(false);
    }

    public void CancelActiveActions(bool restoreIdleState = true)
    {
        StopAllCoroutines();
        isActing = false;
        isDefending = false;
        isDodging = false;
        isHoldingGuard = false;
        isCharging = false;
        chargeHoldTime = 0f;
        transform.position = restPosition;

        if (IsDead)
            SetState(FighterCombatState.Dead);
        else if (restoreIdleState && combatEnabled)
            SetState(FighterCombatState.Idle);
    }

    private IEnumerator LightAttackRoutine()
    {
        isActing = true;
        SetState(FighterCombatState.Attacking);

        Vector3 startPosition = transform.position;
        Vector3 attackPosition = Vector3.MoveTowards(
            startPosition,
            targetStats.transform.position,
            1f
        );

        const float duration = 0.15f;
        yield return MoveBetween(startPosition, attackPosition, duration);

        CombatHitResult hitResult = ResolveAttack();
        OnAttackResolved?.Invoke(targetCombat, hitResult);

        yield return MoveBetween(attackPosition, startPosition, duration);

        transform.position = restPosition;
        isActing = false;
        SetIdleIfAvailable();
    }

    private CombatHitResult ResolveAttack()
    {
        if (targetCombat != null && targetCombat.IsDodging)
            return CombatHitResult.Dodged;

        if (targetCombat != null && targetCombat.IsDefending)
            return CombatHitResult.Blocked;

        targetStats.TakeDamage(lightAttackDamage);
        return CombatHitResult.Hit;
    }

    private IEnumerator DefenseRoutine()
    {
        isActing = true;
        isDefending = true;
        SetState(FighterCombatState.Defending);

        yield return new WaitForSeconds(defenseDuration);

        isDefending = false;
        isActing = false;
        SetIdleIfAvailable();
    }

    private void UpdateHeldGuard()
    {
        if (!isHoldingGuard || !combatEnabled)
            return;

        float cost =
            heldGuardStaminaCostPerSecond * Time.deltaTime;
        if (!fighterStats.SpendStamina(cost))
            StopHeldGuard();
    }

    private void UpdateCharge()
    {
        if (!isCharging || !combatEnabled)
            return;

        chargeHoldTime += Time.deltaTime;
        if (chargeHoldTime < chargeStartupDelay)
            return;

        fighterStats.RecoverStaminaFromCharge(
            chargeRecoveryPerSecond * Time.deltaTime
        );
    }

    private CombatActionResult StartDodge(float direction)
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (isActing || isHoldingGuard || isCharging)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(dodgeStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        StartCoroutine(DodgeRoutine(direction));
        return CombatActionResult.Started;
    }

    private IEnumerator DodgeRoutine(float direction)
    {
        isActing = true;
        isDodging = true;
        SetState(FighterCombatState.Dodging);

        Vector3 startPosition = transform.position;
        Vector3 sideDirection = transform.right * direction;
        Vector3 dodgePosition =
            startPosition + sideDirection * dodgeDistance;

        float halfDuration = dodgeDuration * 0.5f;
        yield return MoveBetween(
            startPosition,
            dodgePosition,
            halfDuration
        );
        yield return MoveBetween(
            dodgePosition,
            startPosition,
            halfDuration
        );

        transform.position = restPosition;
        isDodging = false;
        isActing = false;
        SetIdleIfAvailable();
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

    private bool CanStartAction()
    {
        return combatEnabled &&
               fighterStats != null &&
               !fighterStats.IsDead;
    }

    private void HandleDeath(FighterStats deadStats)
    {
        combatEnabled = false;
        CancelActiveActions(false);
        SetState(FighterCombatState.Dead);
    }

    private void SetIdleIfAvailable()
    {
        if (combatEnabled && !IsDead)
            SetState(FighterCombatState.Idle);
    }

    private void SetState(FighterCombatState state)
    {
        if (CurrentState == state)
            return;

        CurrentState = state;
        OnStateChanged?.Invoke(this, state);
    }
}
