using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
    AttackStartup,
    Attacking,
    Recovering,
    Defending,
    Dodging,
    Charging,
    Stunned,
    Dead
}

public enum CombatHitResult
{
    Hit,
    Blocked,
    PerfectGuard,
    Dodged,
    PerfectDodge
}

public readonly struct CombatImpact
{
    public FighterCombat Attacker { get; }
    public FighterCombat Target { get; }
    public CombatHitResult Result { get; }
    public float ImpactTime { get; }

    public CombatImpact(
        FighterCombat attacker,
        FighterCombat target,
        CombatHitResult result,
        float impactTime)
    {
        Attacker = attacker;
        Target = target;
        Result = result;
        ImpactTime = impactTime;
    }
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
    [Min(0f)]
    [SerializeField] private float attackStartupDuration = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float attackLungeDuration = 0.15f;
    [Min(0.01f)]
    [SerializeField] private float attackReturnDuration = 0.15f;
    [Min(0f)]
    [SerializeField] private float attackRecoveryDuration = 0.5f;
    [Min(0f)]
    [SerializeField] private float perfectGuardStunDuration = 0.22f;
    [Min(0f)]
    [SerializeField] private float perfectDodgeStunDuration = 0.32f;

    [Header("Charge")]
    [Min(0f)]
    [SerializeField] private float chargeStartupDelay = 0.3f;
    [Min(0f)]
    [SerializeField] private float chargeRecoveryPerSecond = 25f;

    [Header("Defense simple")]
    [Min(0.01f)]
    [SerializeField] private float defenseDuration = 0.8f;
    [Min(0f)]
    [SerializeField] private float defenseStaminaCost = 10f;
    [Min(0f)]
    [SerializeField] private float perfectGuardWindow = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float perfectGuardRefundRatio = 0.5f;

    [Header("Garde maintenue")]
    [Min(0f)]
    [SerializeField] private float heldGuardStaminaCostPerSecond = 15f;

    [Header("Esquive")]
    [Min(0f)]
    [SerializeField] private float dodgeStaminaCost = 20f;
    [Min(0f)]
    [SerializeField] private float dodgeDistance = 1.25f;
    [FormerlySerializedAs("dodgeDuration")]
    [Min(0.01f)]
    [SerializeField] private float dodgeMovementDuration = 0.18f;
    [Min(0.01f)]
    [SerializeField] private float dodgeActiveDuration = 0.4f;
    [Min(0f)]
    [SerializeField] private float perfectDodgeWindow = 0.2f;
    [Min(0f)]
    [SerializeField] private float dodgeRecoveryDuration = 0.12f;

    public event Action<FighterCombat, FighterCombatState>
        OnStateChanged;
    public event Action<CombatImpact> OnAttackResolved;

    public FighterStats Stats => fighterStats;
    public bool IsDefending =>
        CurrentState == FighterCombatState.Defending;
    public bool IsDodging =>
        CurrentState == FighterCombatState.Dodging;
    public bool IsCharging =>
        CurrentState == FighterCombatState.Charging;
    public bool IsBusy =>
        CurrentState != FighterCombatState.Idle &&
        CurrentState != FighterCombatState.Dead;
    public bool IsDead =>
        fighterStats == null || fighterStats.IsDead;
    public bool IsPlayerControlled => controlledByPlayer;
    public float LightAttackStaminaCost =>
        lightAttackStaminaCost;
    public FighterCombatState CurrentState { get; private set; }

    private bool combatEnabled = true;
    private bool heldGuardActive;
    private float chargeHoldTime;
    private float defenseStartedAt = float.NegativeInfinity;
    private float dodgeStartedAt = float.NegativeInfinity;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Awake()
    {
        if (fighterStats == null)
            fighterStats = GetComponent<FighterStats>();

        if (targetCombat == null && targetStats != null)
            targetCombat = targetStats.GetComponent<FighterCombat>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
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
        if (CurrentState == FighterCombatState.Defending &&
            heldGuardActive)
        {
            UpdateHeldGuard();
        }
        else if (CurrentState == FighterCombatState.Charging)
        {
            UpdateCharge();
        }
    }

    public CombatActionResult LightAttack(
        float startupDurationOverride = -1f)
    {
        if (!CanStartAction() ||
            targetStats == null ||
            targetStats.IsDead)
        {
            return CombatActionResult.Unavailable;
        }

        if (CurrentState != FighterCombatState.Idle)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(lightAttackStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        float startup = startupDurationOverride >= 0f
            ? startupDurationOverride
            : attackStartupDuration;

        StartCoroutine(LightAttackRoutine(startup));
        return CombatActionResult.Started;
    }

    public CombatActionResult StartDefense()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (CurrentState != FighterCombatState.Idle)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(defenseStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        heldGuardActive = false;
        defenseStartedAt = Time.time;
        StartCoroutine(DefenseRoutine());
        return CombatActionResult.Started;
    }

    public CombatActionResult StartHeldGuard()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (CurrentState != FighterCombatState.Idle)
            return CombatActionResult.Busy;

        float initialCost =
            heldGuardStaminaCostPerSecond *
            Time.unscaledDeltaTime;
        if (!fighterStats.SpendStamina(initialCost))
            return CombatActionResult.NotEnoughStamina;

        heldGuardActive = true;
        defenseStartedAt = Time.time;
        SetState(FighterCombatState.Defending);
        return CombatActionResult.Started;
    }

    public void StopHeldGuard()
    {
        if (!heldGuardActive)
            return;

        heldGuardActive = false;
        if (CurrentState == FighterCombatState.Defending)
            SetState(FighterCombatState.Idle);
    }

    public CombatActionResult StartCharge()
    {
        if (!CanStartAction())
            return CombatActionResult.Unavailable;

        if (CurrentState == FighterCombatState.Charging)
            return CombatActionResult.Started;

        if (CurrentState != FighterCombatState.Idle)
            return CombatActionResult.Busy;

        chargeHoldTime = 0f;
        SetState(FighterCombatState.Charging);
        return CombatActionResult.Started;
    }

    public void StopChargeInput()
    {
        chargeHoldTime = 0f;
        if (CurrentState == FighterCombatState.Charging)
            SetState(FighterCombatState.Idle);
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
            CancelActiveActions(true);
    }

    public void ResetCombatState()
    {
        StopAllCoroutines();
        combatEnabled = true;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        defenseStartedAt = float.NegativeInfinity;
        dodgeStartedAt = float.NegativeInfinity;
        transform.SetPositionAndRotation(
            initialPosition,
            initialRotation
        );
        ForceState(FighterCombatState.Idle);
    }

    public void CancelActiveActions(bool restoreNeutralTransform)
    {
        StopAllCoroutines();
        heldGuardActive = false;
        chargeHoldTime = 0f;

        if (restoreNeutralTransform)
        {
            transform.SetPositionAndRotation(
                initialPosition,
                initialRotation
            );
        }

        ForceState(
            IsDead
                ? FighterCombatState.Dead
                : FighterCombatState.Idle
        );
    }

    private IEnumerator LightAttackRoutine(float startupDuration)
    {
        SetState(FighterCombatState.AttackStartup);

        Vector3 startPosition = initialPosition;
        Vector3 attackPosition = Vector3.MoveTowards(
            startPosition,
            targetStats.transform.position,
            1f
        );

        float safeStartup = Mathf.Max(0f, startupDuration);
        float lungeDuration = Mathf.Min(
            attackLungeDuration,
            safeStartup
        );
        float anticipationDuration =
            Mathf.Max(0f, safeStartup - lungeDuration);

        if (anticipationDuration > 0f)
            yield return new WaitForSeconds(anticipationDuration);

        if (lungeDuration > 0f)
        {
            yield return MoveBetween(
                startPosition,
                attackPosition,
                lungeDuration
            );
        }
        else
        {
            transform.position = attackPosition;
        }

        SetState(FighterCombatState.Attacking);
        float impactTime = Time.time;
        CombatHitResult hitResult = ResolveAttack(impactTime);
        OnAttackResolved?.Invoke(
            new CombatImpact(
                this,
                targetCombat,
                hitResult,
                impactTime
            )
        );

        yield return MoveBetween(
            transform.position,
            initialPosition,
            attackReturnDuration
        );
        transform.position = initialPosition;

        float stunDuration = hitResult switch
        {
            CombatHitResult.PerfectGuard =>
                perfectGuardStunDuration,
            CombatHitResult.PerfectDodge =>
                perfectDodgeStunDuration,
            _ => 0f
        };

        if (stunDuration > 0f)
        {
            SetState(FighterCombatState.Stunned);
            yield return new WaitForSeconds(stunDuration);
        }

        SetState(FighterCombatState.Recovering);
        if (attackRecoveryDuration > 0f)
            yield return new WaitForSeconds(
                attackRecoveryDuration
            );

        SetIdleIfAvailable();
    }

    private CombatHitResult ResolveAttack(float impactTime)
    {
        if (targetCombat != null)
            return targetCombat.ResolveIncomingAttack(
                impactTime,
                lightAttackDamage
            );

        targetStats.TakeDamage(lightAttackDamage);
        return CombatHitResult.Hit;
    }

    private CombatHitResult ResolveIncomingAttack(
        float impactTime,
        float incomingDamage)
    {
        if (CurrentState == FighterCombatState.Dodging)
        {
            float dodgeElapsed = impactTime - dodgeStartedAt;
            float perfectCenter = dodgeActiveDuration * 0.5f;
            float halfWindow = perfectDodgeWindow * 0.5f;

            if (Mathf.Abs(dodgeElapsed - perfectCenter) <=
                halfWindow)
            {
                return CombatHitResult.PerfectDodge;
            }

            return CombatHitResult.Dodged;
        }

        if (CurrentState == FighterCombatState.Defending)
        {
            bool isPerfect =
                !heldGuardActive &&
                impactTime >= defenseStartedAt &&
                impactTime - defenseStartedAt <=
                perfectGuardWindow;

            if (isPerfect)
            {
                fighterStats.RecoverStamina(
                    defenseStaminaCost *
                    perfectGuardRefundRatio
                );
                return CombatHitResult.PerfectGuard;
            }

            return CombatHitResult.Blocked;
        }

        fighterStats.TakeDamage(incomingDamage);
        return CombatHitResult.Hit;
    }

    private IEnumerator DefenseRoutine()
    {
        SetState(FighterCombatState.Defending);
        yield return new WaitForSeconds(defenseDuration);

        if (!heldGuardActive &&
            CurrentState == FighterCombatState.Defending)
        {
            SetState(FighterCombatState.Idle);
        }
    }

    private void UpdateHeldGuard()
    {
        if (!combatEnabled)
            return;

        float cost =
            heldGuardStaminaCostPerSecond * Time.deltaTime;
        if (!fighterStats.SpendStamina(cost))
            StopHeldGuard();
    }

    private void UpdateCharge()
    {
        if (!combatEnabled)
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

        if (CurrentState != FighterCombatState.Idle)
            return CombatActionResult.Busy;

        if (!fighterStats.SpendStamina(dodgeStaminaCost))
            return CombatActionResult.NotEnoughStamina;

        dodgeStartedAt = Time.time;
        StartCoroutine(DodgeRoutine(direction));
        return CombatActionResult.Started;
    }

    private IEnumerator DodgeRoutine(float direction)
    {
        SetState(FighterCombatState.Dodging);

        Vector3 startPosition = initialPosition;
        Vector3 sideDirection = transform.right * direction;
        Vector3 dodgePosition =
            startPosition + sideDirection * dodgeDistance;

        float movementDuration =
            Mathf.Max(0.01f, dodgeMovementDuration);
        float halfDuration = movementDuration * 0.5f;

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
        transform.position = initialPosition;

        float remainingActiveTime =
            Mathf.Max(0f, dodgeActiveDuration - movementDuration);
        if (remainingActiveTime > 0f)
            yield return new WaitForSeconds(remainingActiveTime);

        SetState(FighterCombatState.Recovering);
        if (dodgeRecoveryDuration > 0f)
            yield return new WaitForSeconds(
                dodgeRecoveryDuration
            );

        SetIdleIfAvailable();
    }

    private IEnumerator MoveBetween(
        Vector3 from,
        Vector3 to,
        float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(
                from,
                to,
                Mathf.Clamp01(elapsed / safeDuration)
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
        CancelActiveActions(true);
        ForceState(FighterCombatState.Dead);
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

    private void ForceState(FighterCombatState state)
    {
        CurrentState = state;
        OnStateChanged?.Invoke(this, state);
    }
}
