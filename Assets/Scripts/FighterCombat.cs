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

public enum FighterStunReason
{
    None,
    Countered,
    GuardBreak
}

public enum CombatHitResult
{
    Hit,
    Missed,
    Blocked,
    GuardBroken,
    PerfectGuard,
    Dodged,
    PerfectDodge,
    Interrupted
}

public enum DodgeWindowPhase
{
    None,
    StartupVulnerable,
    Invulnerable,
    Perfect,
    RecoveryVulnerable
}

public readonly struct CombatImpact
{
    public FighterCombat Attacker { get; }
    public FighterCombat Target { get; }
    public CombatHitResult Result { get; }
    public float ImpactTime { get; }
    public RelativeOrientation Orientation { get; }
    public float PositionalMultiplier { get; }
    public float DamageApplied { get; }

    public CombatImpact(
        FighterCombat attacker,
        FighterCombat target,
        CombatHitResult result,
        float impactTime,
        RelativeOrientation orientation =
            RelativeOrientation.Face,
        float positionalMultiplier = 1f,
        float damageApplied = 0f)
    {
        Attacker = attacker;
        Target = target;
        Result = result;
        ImpactTime = impactTime;
        Orientation = orientation;
        PositionalMultiplier =
            Mathf.Max(0f, positionalMultiplier);
        DamageApplied = Mathf.Max(0f, damageApplied);
    }
}

public readonly struct GuardImpact
{
    public FighterCombat Target { get; }
    public float StaminaDamage { get; }
    public bool GuardBroken { get; }
    public float ImpactTime { get; }

    public GuardImpact(
        FighterCombat target,
        float staminaDamage,
        bool guardBroken,
        float impactTime)
    {
        Target = target;
        StaminaDamage = staminaDamage;
        GuardBroken = guardBroken;
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
    [SerializeField] private CombatRulesConfig combatRules;

    private CombatSpatialController spatialController;

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

    [Header("Esquive")]
    [Min(0f)]
    [SerializeField] private float dodgeStaminaCost = 20f;
    [Min(0f)]
    [SerializeField] private float dodgeDistance = 1.25f;
    [FormerlySerializedAs("dodgeDuration")]
    [Min(0.01f)]
    [SerializeField] private float dodgeMovementDuration = 0.18f;

    public event Action<FighterCombat, FighterCombatState>
        OnStateChanged;
    public event Action<CombatImpact> OnAttackResolved;
    public event Action<GuardImpact> OnGuardImpact;

    public FighterStats Stats => fighterStats;
    public bool IsDefending =>
        CurrentState == FighterCombatState.Defending;
    public bool IsDodging =>
        CurrentState == FighterCombatState.Dodging;
    public bool IsCharging =>
        CurrentState == FighterCombatState.Charging;
    public bool IsHeldGuardActive =>
        CurrentState == FighterCombatState.Defending &&
        heldGuardActive;
    public bool IsBusy =>
        CurrentState != FighterCombatState.Idle &&
        CurrentState != FighterCombatState.Dead;
    public bool IsDead =>
        fighterStats == null || fighterStats.IsDead;
    public bool IsPlayerControlled => controlledByPlayer;
    public float LightAttackStaminaCost =>
        lightAttackStaminaCost;
    public CombatRulesConfig Rules =>
        combatRules != null
            ? combatRules
            : CombatRulesConfig.RuntimeDefault;
    public FighterCombatState CurrentState { get; private set; }
    public FighterStunReason CurrentStunReason
    {
        get;
        private set;
    }
    public float StunRemaining { get; private set; }
    public bool IsRiposteWindowActive =>
        combatEnabled &&
        !IsDead &&
        Time.time < riposteWindowEndsAt;
    public float RiposteWindowRemaining =>
        IsRiposteWindowActive
            ? Mathf.Max(0f, riposteWindowEndsAt - Time.time)
            : 0f;
    public CombatRefusalReason LastRefusalReason
    {
        get;
        private set;
    }
    public int IncomingImpactRevision =>
        incomingImpactRevision;
    public CombatSpatialController SpatialController =>
        spatialController;
    public float DodgeStartupDuration =>
        Rules.DodgeStartupDuration;
    public float DodgeInvulnerabilityDuration =>
        Rules.DodgeInvulnerabilityDuration;
    public float PerfectDodgeWindow =>
        Rules.PerfectDodgeWindow;
    public float DodgeRecoveryDuration =>
        Rules.DodgeRecoveryDuration;
    public DodgeWindowPhase CurrentDodgeWindowPhase
    {
        get
        {
            if (float.IsNegativeInfinity(dodgeStartedAt) ||
                (CurrentState != FighterCombatState.Dodging &&
                 CurrentState != FighterCombatState.Recovering))
            {
                return DodgeWindowPhase.None;
            }

            return GetDodgeWindowPhase(
                Time.time - dodgeStartedAt
            );
        }
    }

    private bool combatEnabled = true;
    private bool heldGuardActive;
    private float chargeHoldTime;
    private float defenseStartedAt = float.NegativeInfinity;
    private float dodgeStartedAt = float.NegativeInfinity;
    private float riposteWindowEndsAt = float.NegativeInfinity;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Coroutine simpleDefenseRoutine;
    private SpatialDodgeTransaction activeSpatialDodge;
    private bool hasActiveSpatialDodge;
    private int incomingImpactRevision;
    private long lastPermutationToken = long.MinValue;

    private void Awake()
    {
        if (fighterStats == null)
            fighterStats = GetComponent<FighterStats>();

        if (targetCombat == null && targetStats != null)
            targetCombat = targetStats.GetComponent<FighterCombat>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        CurrentState = FighterCombatState.Idle;
        CurrentStunReason = FighterStunReason.None;
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

    private void OnDestroy()
    {
        if (spatialController != null)
        {
            spatialController.OnDodgeCancelled -=
                HandleSpatialDodgeCancelled;
        }
    }

    private void Update()
    {
        if (CurrentState == FighterCombatState.Charging)
            UpdateCharge();
    }

    public CombatActionResult LightAttack(
        float startupDurationOverride = -1f)
    {
        ClearRefusal();
        if (!CanStartAction() ||
            targetStats == null ||
            targetStats.IsDead)
        {
            return RefuseUnavailable();
        }

        if (CurrentState != FighterCombatState.Idle)
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );

        if (!fighterStats.SpendStamina(lightAttackStaminaCost))
        {
            return Refuse(
                CombatActionResult.NotEnoughStamina,
                CombatRefusalReason.NotEnoughStamina
            );
        }

        StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        ClearRiposteWindow();
        float startup = startupDurationOverride >= 0f
            ? startupDurationOverride
            : attackStartupDuration;

        StartCoroutine(LightAttackRoutine(startup));
        return CombatActionResult.Started;
    }

    public CombatActionResult StartDefense()
    {
        ClearRefusal();
        if (!CanStartAction())
            return RefuseUnavailable();

        if (CurrentState != FighterCombatState.Idle)
        {
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );
        }

        if (!CanDefendFromCurrentOrientation())
            return CombatActionResult.Unavailable;

        if (!fighterStats.SpendStamina(defenseStaminaCost))
        {
            return Refuse(
                CombatActionResult.NotEnoughStamina,
                CombatRefusalReason.NotEnoughStamina
            );
        }

        StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        ClearRiposteWindow();
        heldGuardActive = false;
        defenseStartedAt = Time.time;
        simpleDefenseRoutine =
            StartCoroutine(DefenseRoutine());
        return CombatActionResult.Started;
    }

    public CombatActionResult StartHeldGuard()
    {
        ClearRefusal();
        if (!CanStartAction())
            return RefuseUnavailable();

        if (CurrentState != FighterCombatState.Idle)
        {
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );
        }

        if (!CanDefendFromCurrentOrientation())
            return CombatActionResult.Unavailable;

        StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        ClearRiposteWindow();
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
        ClearRefusal();
        if (!CanStartAction())
            return RefuseUnavailable();

        if (CurrentState == FighterCombatState.Charging)
            return CombatActionResult.Started;

        if (CurrentState != FighterCombatState.Idle)
        {
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );
        }

        StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        ClearRiposteWindow();
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
        return StartDodge(DodgeDirection.Left);
    }

    public CombatActionResult DodgeRight()
    {
        return StartDodge(DodgeDirection.Right);
    }

    public CombatActionResult DodgeForward()
    {
        return StartDodge(DodgeDirection.Forward);
    }

    public CombatActionResult DodgeBackward()
    {
        return StartDodge(DodgeDirection.Backward);
    }

    public bool CanHitCurrentTarget()
    {
        if (targetCombat == null)
            return targetStats != null;

        if (spatialController != null)
        {
            return spatialController.IsTargetInsideAttackArc(
                this,
                targetCombat,
                Rules.AttackHitArc
            );
        }

        return IsInsideAttackArc(
            transform,
            targetCombat.transform,
            Rules.AttackHitArc
        );
    }

    public DodgeWindowPhase GetDodgeWindowPhase(
        float elapsed)
    {
        if (elapsed < 0f)
            return DodgeWindowPhase.None;

        float startup = DodgeStartupDuration;
        if (elapsed < startup)
            return DodgeWindowPhase.StartupVulnerable;

        float invulnerability =
            DodgeInvulnerabilityDuration;
        float invulnerabilityElapsed =
            elapsed - startup;
        if (invulnerabilityElapsed >
            invulnerability)
        {
            return DodgeWindowPhase.RecoveryVulnerable;
        }

        float perfectWindow =
            Mathf.Min(
                PerfectDodgeWindow,
                invulnerability
            );
        float perfectStart =
            (invulnerability - perfectWindow) * 0.5f;
        float perfectEnd =
            perfectStart + perfectWindow;
        if (perfectWindow > 0f &&
            invulnerabilityElapsed >= perfectStart &&
            invulnerabilityElapsed <= perfectEnd)
        {
            return DodgeWindowPhase.Perfect;
        }

        return DodgeWindowPhase.Invulnerable;
    }

    public CombatActionResult StartSpatialMovement(
        SpatialMovementType movement)
    {
        ClearRefusal();
        if (!CanStartAction())
            return RefuseUnavailable();

        if (CurrentState != FighterCombatState.Idle)
        {
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );
        }

        if (spatialController == null ||
            !spatialController.StartMovement(this, movement))
        {
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.IncompatibleOrientation
            );
        }

        spatialController.NotifySignificantAction();
        return CombatActionResult.Started;
    }

    public void StopSpatialMovement()
    {
        spatialController?.StopMovement(this);
    }

    public CombatActionResult TryPermutation(long commandToken)
    {
        ClearRefusal();
        if (!combatEnabled)
            return RefuseUnavailable();
        if (IsDead)
        {
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.Dead
            );
        }
        if (CurrentState == FighterCombatState.Stunned)
        {
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.Stunned
            );
        }
        if (commandToken <= 0 ||
            commandToken <= lastPermutationToken)
        {
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.DuplicateCommand
            );
        }
        if (spatialController == null ||
            !spatialController.CanApplyPermutation)
        {
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.CombatUnavailable
            );
        }

        lastPermutationToken = commandToken;
        float staminaCost =
            Rules.ResolvePermutationStaminaCost();
        if (!fighterStats.SpendStamina(staminaCost))
        {
            return Refuse(
                CombatActionResult.NotEnoughStamina,
                CombatRefusalReason.NotEnoughStamina
            );
        }

        incomingImpactRevision++;
        InterruptForPermutation();
        if (!spatialController.ApplyPermutation(this))
        {
            fighterStats.RecoverStamina(staminaCost);
            return Refuse(
                CombatActionResult.Unavailable,
                CombatRefusalReason.CombatUnavailable
            );
        }

        spatialController.NotifySignificantAction();
        ForceState(FighterCombatState.Idle);
        return CombatActionResult.Started;
    }

    public void SetCombatEnabled(bool enabled)
    {
        if (combatEnabled == enabled)
            return;

        combatEnabled = enabled;
        if (!enabled)
        {
            incomingImpactRevision++;
            CancelActiveActions(false);
        }
    }

    public void SetCombatRules(CombatRulesConfig rules)
    {
        combatRules =
            rules != null
                ? rules
                : CombatRulesConfig.RuntimeDefault;
    }

    public void SetSpatialController(
        CombatSpatialController controller)
    {
        if (spatialController != null)
        {
            spatialController.OnDodgeCancelled -=
                HandleSpatialDodgeCancelled;
        }

        spatialController = controller;
        if (spatialController != null)
        {
            spatialController.OnDodgeCancelled +=
                HandleSpatialDodgeCancelled;
        }
    }

    public void ResetCombatState()
    {
        bool transformNeedsReset =
            spatialController == null &&
            ((transform.position - initialPosition).sqrMagnitude >
                 0.000001f ||
             Quaternion.Angle(
                 transform.rotation,
                 initialRotation
             ) > 0.01f);
        bool alreadyReset =
            combatEnabled &&
            CurrentState == FighterCombatState.Idle &&
            CurrentStunReason == FighterStunReason.None &&
            StunRemaining <= 0f &&
            !heldGuardActive &&
            Mathf.Approximately(chargeHoldTime, 0f) &&
            simpleDefenseRoutine == null &&
            !hasActiveSpatialDodge &&
            float.IsNegativeInfinity(defenseStartedAt) &&
            float.IsNegativeInfinity(dodgeStartedAt) &&
            float.IsNegativeInfinity(riposteWindowEndsAt) &&
            lastPermutationToken == long.MinValue &&
            LastRefusalReason == CombatRefusalReason.None &&
            !transformNeedsReset;
        if (alreadyReset)
            return;

        CancelPendingDodge();
        StopAllCoroutines();
        simpleDefenseRoutine = null;
        combatEnabled = true;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        CurrentStunReason = FighterStunReason.None;
        StunRemaining = 0f;
        defenseStartedAt = float.NegativeInfinity;
        dodgeStartedAt = float.NegativeInfinity;
        incomingImpactRevision++;
        lastPermutationToken = long.MinValue;
        LastRefusalReason = CombatRefusalReason.None;
        ClearRiposteWindow();
        if (spatialController == null)
        {
            transform.SetPositionAndRotation(
                initialPosition,
                initialRotation
            );
        }
        if (CurrentState != FighterCombatState.Idle)
            ForceState(FighterCombatState.Idle);
    }

    public void CancelActiveActions(bool restoreNeutralTransform)
    {
        CancelPendingDodge();
        StopAllCoroutines();
        simpleDefenseRoutine = null;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        CurrentStunReason = FighterStunReason.None;
        StunRemaining = 0f;
        defenseStartedAt = float.NegativeInfinity;
        dodgeStartedAt = float.NegativeInfinity;
        ClearRiposteWindow();
        StopSpatialMovement();

        if (spatialController != null)
        {
            spatialController.RestoreNeutralPose(this);
        }
        else if (restoreNeutralTransform)
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

        int expectedTargetImpactRevision =
            targetCombat != null
                ? targetCombat.IncomingImpactRevision
                : 0;

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
            yield return MoveAttackTowardTarget(
                lungeDuration,
                expectedTargetImpactRevision
            );
        }
        else if (!HasTargetImpactRevisionChanged(
                     expectedTargetImpactRevision))
        {
            transform.position =
                GetNeutralPosition() +
                GetCurrentAttackOffset();
        }

        SetState(FighterCombatState.Attacking);
        float impactTime = Time.time;
        CombatHitResult hitResult = ResolveAttack(
            impactTime,
            expectedTargetImpactRevision,
            out RelativeOrientation orientation,
            out float positionalMultiplier,
            out float damageApplied
        );
        OnAttackResolved?.Invoke(
            new CombatImpact(
                this,
                targetCombat,
                hitResult,
                impactTime,
                orientation,
                positionalMultiplier,
                damageApplied
            )
        );

        yield return ReturnAttackToNeutral(
            attackReturnDuration,
            expectedTargetImpactRevision
        );
        RestoreNeutralPose();

        float stunDuration = hitResult switch
        {
            CombatHitResult.PerfectDodge =>
                perfectDodgeStunDuration,
            _ => 0f
        };

        if (stunDuration > 0f)
        {
            CurrentStunReason =
                FighterStunReason.Countered;
            StunRemaining = stunDuration;
            SetState(FighterCombatState.Stunned);
            yield return new WaitForSeconds(stunDuration);
            CurrentStunReason =
                FighterStunReason.None;
            StunRemaining = 0f;
        }

        SetState(FighterCombatState.Recovering);
        if (attackRecoveryDuration > 0f)
            yield return new WaitForSeconds(
                attackRecoveryDuration
            );

        SetIdleIfAvailable();
    }

    private CombatHitResult ResolveAttack(
        float impactTime,
        int expectedTargetImpactRevision,
        out RelativeOrientation orientation,
        out float positionalMultiplier,
        out float damageApplied)
    {
        orientation = spatialController != null &&
            targetCombat != null
                ? spatialController.GetAttackOrientation(
                    this,
                    targetCombat
                )
                : RelativeOrientation.Face;
        float finalDamage = orientation switch
        {
            RelativeOrientation.LeftFlank =>
                Rules.ResolveFlankDamage(lightAttackDamage),
            RelativeOrientation.RightFlank =>
                Rules.ResolveFlankDamage(lightAttackDamage),
            RelativeOrientation.Back =>
                Rules.ResolveBackDamage(lightAttackDamage),
            _ => CombatRulesConfig.ResolvePositionalDamage(
                lightAttackDamage,
                1f
            )
        };
        positionalMultiplier =
            lightAttackDamage > Mathf.Epsilon
                ? finalDamage / lightAttackDamage
                : 1f;
        damageApplied = 0f;

        if (targetCombat != null)
        {
            if (targetCombat.IncomingImpactRevision !=
                expectedTargetImpactRevision)
            {
                return CombatHitResult.Interrupted;
            }

            if (!CanHitCurrentTarget())
                return CombatHitResult.Missed;

            CombatHitResult result =
                targetCombat.ResolveIncomingAttack(
                impactTime,
                finalDamage,
                orientation
            );
            if (result == CombatHitResult.Hit)
                damageApplied = finalDamage;
            return result;
        }

        targetStats.TakeDamage(finalDamage);
        damageApplied = finalDamage;
        return CombatHitResult.Hit;
    }

    private CombatHitResult ResolveIncomingAttack(
        float impactTime,
        float incomingDamage,
        RelativeOrientation incomingOrientation)
    {
        if (CurrentState == FighterCombatState.Dodging)
        {
            float dodgeElapsed = impactTime - dodgeStartedAt;
            DodgeWindowPhase phase =
                GetDodgeWindowPhase(dodgeElapsed);

            if (phase == DodgeWindowPhase.Perfect)
            {
                return CombatHitResult.PerfectDodge;
            }

            if (phase == DodgeWindowPhase.Invulnerable)
                return CombatHitResult.Dodged;
        }

        bool defenseAllowed =
            incomingOrientation == RelativeOrientation.Face;
        if (CurrentState == FighterCombatState.Defending &&
            defenseAllowed)
        {
            bool isPerfect =
                !heldGuardActive &&
                impactTime >= defenseStartedAt &&
                impactTime - defenseStartedAt <=
                perfectGuardWindow;

            if (isPerfect)
            {
                CompletePerfectGuard();
                fighterStats.RecoverStamina(
                    defenseStaminaCost *
                    perfectGuardRefundRatio
                );
                return CombatHitResult.PerfectGuard;
            }

            if (heldGuardActive)
            {
                return ResolveHeldGuardImpact(
                    impactTime
                );
            }

            return CombatHitResult.Blocked;
        }

        if (CurrentState == FighterCombatState.Defending)
        {
            heldGuardActive = false;
            defenseStartedAt = float.NegativeInfinity;
            if (simpleDefenseRoutine != null)
            {
                StopCoroutine(simpleDefenseRoutine);
                simpleDefenseRoutine = null;
            }
            ForceState(FighterCombatState.Idle);
        }

        StopSpatialMovement();
        fighterStats.TakeDamage(incomingDamage);
        spatialController?.NotifySignificantAction();
        return CombatHitResult.Hit;
    }

    private CombatHitResult ResolveHeldGuardImpact(
        float impactTime)
    {
        float staminaDamage =
            Rules.ResolveGuardStaminaDamage();
        float appliedStaminaDamage =
            fighterStats.ApplyStaminaDamage(
                staminaDamage
            );

        bool guardBroken =
            fighterStats.CurrentStamina <= Mathf.Epsilon;
        if (guardBroken)
            BeginGuardBreakStun();

        OnGuardImpact?.Invoke(
            new GuardImpact(
                this,
                appliedStaminaDamage,
                guardBroken,
                impactTime
            )
        );

        return guardBroken
            ? CombatHitResult.GuardBroken
            : CombatHitResult.Blocked;
    }

    private IEnumerator DefenseRoutine()
    {
        SetState(FighterCombatState.Defending);
        yield return new WaitForSeconds(defenseDuration);

        simpleDefenseRoutine = null;
        if (!heldGuardActive &&
            CurrentState == FighterCombatState.Defending)
        {
            SetState(FighterCombatState.Idle);
        }
    }

    private void CompletePerfectGuard()
    {
        if (simpleDefenseRoutine != null)
        {
            StopCoroutine(simpleDefenseRoutine);
            simpleDefenseRoutine = null;
        }

        heldGuardActive = false;
        defenseStartedAt = float.NegativeInfinity;
        riposteWindowEndsAt =
            Time.time + Rules.RiposteWindowDuration;
        SetState(FighterCombatState.Idle);
    }

    private void ClearRiposteWindow()
    {
        riposteWindowEndsAt = float.NegativeInfinity;
    }

    private void BeginGuardBreakStun()
    {
        if (CurrentState == FighterCombatState.Stunned &&
            CurrentStunReason ==
            FighterStunReason.GuardBreak)
        {
            return;
        }

        StopSpatialMovement();
        CancelPendingDodge();
        incomingImpactRevision++;
        StopAllCoroutines();
        simpleDefenseRoutine = null;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        defenseStartedAt = float.NegativeInfinity;
        dodgeStartedAt = float.NegativeInfinity;
        ClearRiposteWindow();
        fighterStats.SetStamina(0f);

        CurrentStunReason =
            FighterStunReason.GuardBreak;
        StunRemaining =
            Rules.GuardBreakStunDuration;
        SetState(FighterCombatState.Stunned);

        StartCoroutine(GuardBreakStunRoutine());
    }

    private IEnumerator GuardBreakStunRoutine()
    {
        float duration =
            Rules.GuardBreakStunDuration;
        float targetStamina = Mathf.Min(
            Rules.StunRecoveryStamina,
            fighterStats.MaxStamina
        );
        AnimationCurve recoveryCurve =
            Rules.StunRecoveryCurve;
        recoveryCurve ??=
            AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f
            );
        float elapsed = 0f;

        while (elapsed < duration &&
               combatEnabled &&
               !IsDead &&
               CurrentState ==
               FighterCombatState.Stunned &&
               CurrentStunReason ==
               FighterStunReason.GuardBreak)
        {
            float normalizedTime =
                Mathf.Clamp01(elapsed / duration);
            float progress = Mathf.Clamp01(
                recoveryCurve.Evaluate(normalizedTime)
            );

            fighterStats.SetStamina(
                targetStamina * progress
            );
            StunRemaining =
                Mathf.Max(0f, duration - elapsed);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        if (!combatEnabled ||
            IsDead ||
            CurrentState != FighterCombatState.Stunned ||
            CurrentStunReason !=
            FighterStunReason.GuardBreak)
        {
            yield break;
        }

        fighterStats.SetStamina(targetStamina);
        StunRemaining = 0f;
        CurrentStunReason =
            FighterStunReason.None;
        SetState(FighterCombatState.Idle);
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

    private CombatActionResult StartDodge(
        DodgeDirection direction)
    {
        ClearRefusal();
        if (!CanStartAction())
            return RefuseUnavailable();

        if (CurrentState != FighterCombatState.Idle)
        {
            return Refuse(
                CombatActionResult.Busy,
                CombatRefusalReason.Busy
            );
        }

        if (fighterStats.CurrentStamina + Mathf.Epsilon <
            dodgeStaminaCost)
        {
            return Refuse(
                CombatActionResult.NotEnoughStamina,
                CombatRefusalReason.NotEnoughStamina
            );
        }

        SpatialDodgeTransaction preparedDodge = default;
        if (spatialController != null &&
            !spatialController.TryPrepareDodge(
                this,
                direction,
                out preparedDodge
            ))
        {
            CombatRefusalReason reason =
                direction is DodgeDirection.Forward or
                    DodgeDirection.Backward
                    ? CombatRefusalReason.DistanceLimit
                    : CombatRefusalReason.IncompatibleOrientation;
            return Refuse(
                CombatActionResult.Unavailable,
                reason
            );
        }

        if (!fighterStats.SpendStamina(dodgeStaminaCost))
        {
            if (preparedDodge.IsValid)
                spatialController.CancelDodge(preparedDodge);
            return Refuse(
                CombatActionResult.NotEnoughStamina,
                CombatRefusalReason.NotEnoughStamina
            );
        }

        StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        ClearRiposteWindow();
        dodgeStartedAt = Time.time;
        activeSpatialDodge = preparedDodge;
        hasActiveSpatialDodge = preparedDodge.IsValid;
        StartCoroutine(DodgeRoutine(direction));
        return CombatActionResult.Started;
    }

    private IEnumerator DodgeRoutine(
        DodgeDirection direction)
    {
        SetState(FighterCombatState.Dodging);

        float movementDuration = spatialController != null &&
            hasActiveSpatialDodge
                ? ResolveSpatialDodgeDuration(
                    activeSpatialDodge
                )
                : Mathf.Max(0.01f, dodgeMovementDuration);

        if (hasActiveSpatialDodge)
        {
            float elapsed = 0f;
            while (elapsed < movementDuration)
            {
                elapsed += Time.deltaTime;
                spatialController.PreviewPreparedDodge(
                    activeSpatialDodge.Id,
                    Mathf.Clamp01(
                        elapsed / movementDuration
                    )
                );
                yield return null;
            }

            spatialController.PreviewPreparedDodge(
                activeSpatialDodge.Id,
                1f
            );
            spatialController.CommitDodge(
                activeSpatialDodge
            );
            hasActiveSpatialDodge = false;
            activeSpatialDodge = default;
        }
        else
        {
            Vector3 startPosition = transform.position;
            float signedDirection =
                direction == DodgeDirection.Left
                    ? -1f
                    : 1f;
            Vector3 sideDirection =
                transform.right * signedDirection;
            Vector3 dodgePosition =
                startPosition +
                sideDirection * dodgeDistance;
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
            transform.position = startPosition;
        }

        float protectionEnd =
            DodgeStartupDuration +
            DodgeInvulnerabilityDuration;
        float remainingProtectionTime =
            Mathf.Max(0f, protectionEnd - movementDuration);
        if (remainingProtectionTime > 0f)
        {
            yield return new WaitForSeconds(
                remainingProtectionTime
            );
        }

        SetState(FighterCombatState.Recovering);
        if (DodgeRecoveryDuration > 0f)
            yield return new WaitForSeconds(
                DodgeRecoveryDuration
            );

        dodgeStartedAt = float.NegativeInfinity;
        SetIdleIfAvailable();
    }

    private IEnumerator MoveAttackTowardTarget(
        float duration,
        int expectedTargetImpactRevision)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            if (HasTargetImpactRevisionChanged(
                    expectedTargetImpactRevision))
            {
                RestoreNeutralPose();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress =
                Mathf.Clamp01(elapsed / safeDuration);
            transform.position =
                GetNeutralPosition() +
                GetCurrentAttackOffset() * progress;
            yield return null;
        }
    }

    private IEnumerator ReturnAttackToNeutral(
        float duration,
        int expectedTargetImpactRevision)
    {
        Vector3 initialOffset =
            transform.position - GetNeutralPosition();
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            if (HasTargetImpactRevisionChanged(
                    expectedTargetImpactRevision))
            {
                RestoreNeutralPose();
                yield break;
            }

            elapsed += Time.deltaTime;
            transform.position =
                GetNeutralPosition() +
                Vector3.Lerp(
                    initialOffset,
                    Vector3.zero,
                    Mathf.Clamp01(elapsed / safeDuration)
                );
            yield return null;
        }
    }

    private Vector3 GetCurrentAttackOffset()
    {
        Quaternion neutralRotation = transform.rotation;
        if (spatialController != null &&
            spatialController.TryGetNeutralPose(
                this,
                out Pose neutralPose
            ))
        {
            neutralRotation = neutralPose.rotation;
        }

        Vector3 forward = Vector3.ProjectOnPlane(
            neutralRotation * Vector3.forward,
            Vector3.up
        );
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private static bool IsInsideAttackArc(
        Transform attacker,
        Transform target,
        float fullArc)
    {
        Vector3 forward = Vector3.ProjectOnPlane(
            attacker.forward,
            Vector3.up
        );
        Vector3 toTarget = Vector3.ProjectOnPlane(
            target.position - attacker.position,
            Vector3.up
        );
        if (forward.sqrMagnitude <= 0.0001f ||
            toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float halfArc =
            Mathf.Clamp(fullArc, 1f, 360f) * 0.5f;
        return Vector3.Angle(forward, toTarget) <= halfArc;
    }

    private bool HasTargetImpactRevisionChanged(
        int expectedTargetImpactRevision)
    {
        return targetCombat != null &&
               targetCombat.IncomingImpactRevision !=
               expectedTargetImpactRevision;
    }

    private float ResolveSpatialDodgeDuration(
        SpatialDodgeTransaction transaction)
    {
        float travelDistance = Mathf.Max(
            Vector3.Distance(
                transaction.FirstStartPose.position,
                transaction.FirstEndPose.position
            ),
            Vector3.Distance(
                transaction.SecondStartPose.position,
                transaction.SecondEndPose.position
            )
        );
        float rotationAngle = Mathf.Max(
            Quaternion.Angle(
                transaction.FirstStartPose.rotation,
                transaction.FirstEndPose.rotation
            ),
            Quaternion.Angle(
                transaction.SecondStartPose.rotation,
                transaction.SecondEndPose.rotation
            )
        );

        return Mathf.Max(
            Rules.DodgeSpatialDuration,
            travelDistance / Rules.DodgeSpatialSpeed,
            rotationAngle / Rules.RotationSpeed,
            0.01f
        );
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
        incomingImpactRevision++;
        CancelActiveActions(false);
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
        if (state != FighterCombatState.Idle)
            StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        OnStateChanged?.Invoke(this, state);
    }

    private void ForceState(FighterCombatState state)
    {
        if (CurrentState == state)
            return;

        CurrentState = state;
        if (state != FighterCombatState.Idle)
            StopSpatialMovement();
        spatialController?.NotifySignificantAction();
        OnStateChanged?.Invoke(this, state);
    }

    private bool CanDefendFromCurrentOrientation()
    {
        if (spatialController == null ||
            targetCombat == null)
        {
            return true;
        }

        RelativeOrientation orientation =
            spatialController.GetAttackOrientation(
                targetCombat,
                this
            );
        if (orientation == RelativeOrientation.Face)
            return true;

        LastRefusalReason =
            orientation == RelativeOrientation.Back
                ? CombatRefusalReason.BackGuardForbidden
                : CombatRefusalReason.FlankGuardForbidden;
        return false;
    }

    private void InterruptForPermutation()
    {
        CancelPendingDodge();
        StopAllCoroutines();
        simpleDefenseRoutine = null;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        defenseStartedAt = float.NegativeInfinity;
        dodgeStartedAt = float.NegativeInfinity;
        ClearRiposteWindow();
        StopSpatialMovement();
    }

    private void CancelPendingDodge()
    {
        if (!hasActiveSpatialDodge)
            return;

        spatialController?.CancelDodge(activeSpatialDodge);
        hasActiveSpatialDodge = false;
        activeSpatialDodge = default;
    }

    private void HandleSpatialDodgeCancelled(
        SpatialDodgeTransaction transaction)
    {
        if (!hasActiveSpatialDodge ||
            transaction.Fighter != this ||
            transaction.Id != activeSpatialDodge.Id)
        {
            return;
        }

        StopAllCoroutines();
        simpleDefenseRoutine = null;
        heldGuardActive = false;
        chargeHoldTime = 0f;
        dodgeStartedAt = float.NegativeInfinity;
        hasActiveSpatialDodge = false;
        activeSpatialDodge = default;

        if (combatEnabled &&
            !IsDead &&
            CurrentState != FighterCombatState.Stunned)
        {
            ForceState(FighterCombatState.Idle);
        }
    }

    private Vector3 GetNeutralPosition()
    {
        if (spatialController != null &&
            spatialController.TryGetNeutralPosition(
                this,
                out Vector3 neutralPosition
            ))
        {
            return neutralPosition;
        }

        return initialPosition;
    }

    private void RestoreNeutralPose()
    {
        if (spatialController != null)
        {
            spatialController.RestoreNeutralPose(this);
            return;
        }

        transform.SetPositionAndRotation(
            initialPosition,
            initialRotation
        );
    }

    private void ClearRefusal()
    {
        LastRefusalReason = CombatRefusalReason.None;
    }

    private CombatActionResult Refuse(
        CombatActionResult result,
        CombatRefusalReason reason)
    {
        LastRefusalReason = reason;
        return result;
    }

    private CombatActionResult RefuseUnavailable()
    {
        CombatRefusalReason reason = IsDead
            ? CombatRefusalReason.Dead
            : CurrentState == FighterCombatState.Stunned
                ? CombatRefusalReason.Stunned
                : CombatRefusalReason.CombatUnavailable;
        return Refuse(
            CombatActionResult.Unavailable,
            reason
        );
    }
}
