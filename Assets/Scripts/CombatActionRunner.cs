using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatImpactCandidate
{
    public CombatActionRunner Attacker { get; }
    public CombatActionRunner Target { get; }
    public ResolvedCombatAction Action { get; }
    public int GlobalFrame { get; }
    public int LocalFrame { get; }

    public CombatImpactCandidate(
        CombatActionRunner attacker,
        CombatActionRunner target,
        ResolvedCombatAction action,
        int globalFrame,
        int localFrame)
    {
        Attacker = attacker;
        Target = target;
        Action = action;
        GlobalFrame = globalFrame;
        LocalFrame = localFrame;
    }
}

public sealed class CombatActionRunner
{
    private const int PerfectDodgeCounterStunFrames = 19;

    private readonly CombatFrameDataSettings settings;
    private readonly CombatCommandBuffer commandBuffer;
    private readonly Dictionary<
        CombatActionId,
        CombatActionDefinition> definitions = new();
    private readonly List<ICombatActionModifier> modifiers = new();

    private CombatFrameSystem system;
    private FighterCombat owner;
    private FighterCombat target;
    private FighterStats stats;
    private CombatSpatialController spatial;
    private CombatRulesConfig rules;
    private ResolvedCombatAction currentAction;
    private CombatFrameCommand currentCommand;
    private int localActionFrame = -1;
    private int actionStartGlobalFrame;
    private int hitstopRemaining;
    private int hitstunRemaining;
    private int blockstunRemaining;
    private int guardBreakRemaining;
    private int riposteRemaining;
    private int guardLocalFrame;
    private int simpleGuardRemaining;
    private float guardStartupCostPaid;
    private int rechargeLocalFrame;
    private int currentGlobalFrame;
    private bool combatEnabled = true;
    private bool hitRegistered;
    private bool guardRequested;
    private bool guarding;
    private bool rechargeRequested;
    private bool recharging;
    private bool destinationValidated;
    private bool dodgeInterrupted;
    private bool frozenThisTick;
    private bool pendingAutoFace;
    private long lastPermutationToken = long.MinValue;
    private DodgeDirection currentDodgeDirection;
    private SpatialDodgeTransaction dodgeTransaction;
    private bool hasDodgeTransaction;
    private CombatFrameOutcome lastOutcome;

    public event Action<CombatActionRunner, CombatFrameOutcome>
        OnOutcome;

    public FighterCombat Owner => owner;
    public FighterCombat Target => target;
    public FighterStats Stats => stats;
    public CombatActionId CurrentActionId =>
        currentAction != null
            ? currentAction.Id
            : recharging
                ? CombatActionId.Recharge
                : guarding
                    ? CombatActionId.Guard
                    : CombatActionId.None;
    public CombatActionPhase CurrentPhase { get; private set; }
    public int LocalActionFrame => localActionFrame;
    public int ActionStartGlobalFrame => actionStartGlobalFrame;
    public int HitstopRemaining => hitstopRemaining;
    public int HitstunRemaining => hitstunRemaining;
    public int BlockstunRemaining => blockstunRemaining;
    public int GuardBreakRemaining => guardBreakRemaining;
    public int RiposteRemaining => riposteRemaining;
    public bool IsRiposteWindowActive => riposteRemaining > 0;
    public bool IsInvulnerable =>
        currentAction != null &&
        currentAction.InvulnerabilityWindow.Contains(localActionFrame);
    public bool IsPerfectDodgeWindow =>
        IsDodging &&
        currentAction.PerfectDodgeWindow.Contains(localActionFrame);
    public bool IsDodging =>
        currentAction != null && IsDodge(currentAction.Id);
    public bool IsGuarding =>
        guarding &&
        blockstunRemaining <= 0 &&
        guardBreakRemaining <= 0;
    public bool IsParryActive =>
        IsGuarding &&
        guardLocalFrame >= 0 &&
        guardLocalFrame < settings.ParryActiveFrames;
    public bool IsRecharging => recharging;
    public bool IsBusy =>
        currentAction != null ||
        guarding ||
        recharging ||
        hitstopRemaining > 0 ||
        hitstunRemaining > 0 ||
        blockstunRemaining > 0 ||
        guardBreakRemaining > 0;
    public bool IsInterruptible =>
        currentAction != null ||
        guarding ||
        recharging;
    public bool IsDead => stats == null || stats.IsDead;
    public bool DestinationValidated => destinationValidated;
    public bool DodgeInterrupted => dodgeInterrupted;
    public bool PendingAutoFace
    {
        get => pendingAutoFace;
        set => pendingAutoFace = value;
    }
    public CombatFrameOutcome LastOutcome => lastOutcome;
    public CombatActionId BufferedCommand =>
        commandBuffer.BufferedAction;
    public int BufferRemainingFrames =>
        commandBuffer.RemainingFrames(currentGlobalFrame);

    public CombatActionRunner(CombatFrameDataSettings dataSettings)
    {
        settings = dataSettings ??
            throw new ArgumentNullException(nameof(dataSettings));
        commandBuffer =
            new CombatCommandBuffer(settings.InputBufferFrames);
        BuildDefinitions();
    }

    public void Initialize(
        CombatFrameSystem frameSystem,
        FighterCombat fighter,
        FighterCombat targetFighter,
        CombatSpatialController spatialAuthority,
        CombatRulesConfig combatRules)
    {
        system = frameSystem;
        owner = fighter;
        target = targetFighter;
        stats = fighter != null ? fighter.Stats : null;
        spatial = spatialAuthority;
        rules = combatRules ?? CombatRulesConfig.RuntimeDefault;
        Reset(true);
    }

    public CombatActionDefinition GetDefinition(CombatActionId id)
    {
        definitions.TryGetValue(id, out CombatActionDefinition definition);
        return definition;
    }

    public void RegisterModifier(ICombatActionModifier modifier)
    {
        if (modifier == null || modifiers.Contains(modifier))
            return;

        modifiers.Add(modifier);
        modifiers.Sort(
            (left, right) =>
                left.Priority.CompareTo(right.Priority)
        );
    }

    public void UnregisterModifier(ICombatActionModifier modifier)
    {
        if (modifier != null)
            modifiers.Remove(modifier);
    }

    public CombatActionResult Submit(
        CombatActionId actionId,
        long token = 0)
    {
        if (!combatEnabled || IsDead)
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.Unavailable;
        }
        if (hitstunRemaining > 0 ||
            blockstunRemaining > 0 ||
            guardBreakRemaining > 0)
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.Busy;
        }

        CombatFrameCommand command =
            new(actionId, currentGlobalFrame, token);
        if (!command.IsValid || !IsKnownCommand(actionId))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.Unavailable;
        }

        if (CanStartCommandNow(command))
        {
            CombatActionResult validation =
                ValidateImmediateCostAndSpace(command);
            if (validation != CombatActionResult.Started)
                return validation;
        }

        CombatCommandBufferStatus status =
            commandBuffer.Store(command);
        SetOutcome(
            status == CombatCommandBufferStatus.Replaced
                ? CombatFrameOutcome.Replaced
                : CombatFrameOutcome.Buffered
        );
        return CombatActionResult.Started;
    }

    public CombatActionResult BeginHeldGuard()
    {
        guardRequested = true;
        CombatActionResult result = Submit(CombatActionId.Guard);
        if (result != CombatActionResult.Started)
            guardRequested = false;
        return result;
    }

    public void EndHeldGuard()
    {
        guardRequested = false;
        if (guarding && blockstunRemaining <= 0)
            StopGuard();
    }

    public CombatActionResult BeginRecharge()
    {
        rechargeRequested = true;
        CombatActionResult result = Submit(CombatActionId.Recharge);
        if (result != CombatActionResult.Started)
            rechargeRequested = false;
        return result;
    }

    public void EndRecharge()
    {
        rechargeRequested = false;
        if (recharging)
            StopRecharge();
    }

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;
        if (!enabled)
            CancelAll(false);
    }

    public void BeginTick(int globalFrame)
    {
        currentGlobalFrame = globalFrame;
        frozenThisTick = false;

        if (commandBuffer.UpdateExpiration(globalFrame))
            SetOutcome(CombatFrameOutcome.Expired);

        if (!combatEnabled || IsDead)
        {
            if (IsDead)
                CurrentPhase = CombatActionPhase.Dead;
            return;
        }

        if (riposteRemaining > 0)
            riposteRemaining--;

        if (hitstopRemaining > 0)
        {
            hitstopRemaining--;
            frozenThisTick = true;
            CurrentPhase = CombatActionPhase.Hitstop;
            ApplyStateToOwner();
            return;
        }

        if (guardBreakRemaining > 0)
        {
            TickGuardBreak();
            ApplyStateToOwner();
            return;
        }

        if (hitstunRemaining > 0)
        {
            hitstunRemaining--;
            CurrentPhase = CombatActionPhase.Hitstun;
            if (hitstunRemaining == 0)
                CurrentPhase = CombatActionPhase.Idle;
            ApplyStateToOwner();
            return;
        }

        if (blockstunRemaining > 0)
        {
            blockstunRemaining--;
            CurrentPhase = CombatActionPhase.Blockstun;
            if (blockstunRemaining == 0)
            {
                if (guardRequested)
                {
                    guarding = true;
                    guardLocalFrame = settings.ParryActiveFrames;
                    CurrentPhase = CombatActionPhase.Guarding;
                }
                else
                {
                    guarding = false;
                    CurrentPhase = CombatActionPhase.Idle;
                }
            }
            ApplyStateToOwner();
            return;
        }

        if (currentAction == null &&
            !guarding &&
            !recharging &&
            commandBuffer.HasCommand &&
            commandBuffer.TryConsume(
                globalFrame,
                out CombatFrameCommand command))
        {
            StartCommand(command);
        }

        if (guarding)
        {
            TickGuard();
            ApplyStateToOwner();
            return;
        }

        if (recharging)
        {
            TickRecharge();
            ApplyStateToOwner();
            return;
        }

        if (currentAction != null)
        {
            UpdateActionPhase();
            UpdateActionVisuals();
        }
        else
        {
            CurrentPhase = CombatActionPhase.Idle;
        }

        ApplyStateToOwner();
    }

    public bool TryCreateImpactCandidate(
        int globalFrame,
        out CombatImpactCandidate candidate)
    {
        if (!frozenThisTick &&
            currentAction != null &&
            IsAttack(currentAction.Id) &&
            currentAction.BaseDefinition.IsActive(localActionFrame) &&
            !hitRegistered &&
            target != null &&
            !target.IsDead)
        {
            candidate = new CombatImpactCandidate(
                this,
                target.FrameRunner,
                currentAction,
                globalFrame,
                localActionFrame
            );
            return candidate.Target != null;
        }

        candidate = default;
        return false;
    }

    public void EndTick()
    {
        if (!combatEnabled ||
            frozenThisTick ||
            IsDead ||
            hitstopRemaining > 0 ||
            hitstunRemaining > 0 ||
            blockstunRemaining > 0 ||
            guardBreakRemaining > 0)
        {
            return;
        }

        if (guarding)
        {
            guardLocalFrame++;
            if (!guardRequested)
                simpleGuardRemaining--;
            return;
        }

        if (recharging)
        {
            rechargeLocalFrame++;
            return;
        }

        if (currentAction == null)
            return;

        localActionFrame++;
        if (localActionFrame >= currentAction.TotalFrames)
            FinishCurrentAction();
    }

    public void MarkAttackResolved()
    {
        hitRegistered = true;
    }

    public void SetHitstop(int frames)
    {
        hitstopRemaining = Mathf.Max(
            hitstopRemaining,
            Mathf.Max(0, frames)
        );
    }

    public void ApplyHitstun(
        int frames,
        CombatFrameOutcome outcome)
    {
        InterruptCurrentAction(outcome);
        hitstunRemaining = Mathf.Max(1, frames);
        CurrentPhase = CombatActionPhase.Hitstun;
        commandBuffer.Clear();
        ApplyStateToOwner();
    }

    public void ApplyBlockstun(int frames)
    {
        currentAction = null;
        localActionFrame = -1;
        guarding = false;
        recharging = false;
        blockstunRemaining = Mathf.Max(1, frames);
        commandBuffer.Clear();
        CurrentPhase = CombatActionPhase.Blockstun;
        owner?.FrameRestoreNeutralPose();
        ApplyStateToOwner();
    }

    public void ApplyGuardBreak()
    {
        if (guardBreakRemaining > 0)
            return;

        CancelAll(false);
        stats.SetStamina(0f);
        guardBreakRemaining = settings.GuardBreakFrames;
        CurrentPhase = CombatActionPhase.GuardBrokenStun;
        commandBuffer.Clear();
        ApplyStateToOwner();
    }

    public void CompleteParry()
    {
        float refund =
            guardStartupCostPaid *
            settings.ParryStaminaRefundRatio;
        StopGuard();
        stats?.RecoverStamina(refund);
        riposteRemaining = settings.RiposteWindowFrames;
        SetOutcome(CombatFrameOutcome.Parry);
    }

    public void ApplyPerfectDodgeCounter()
    {
        ApplyHitstun(
            PerfectDodgeCounterStunFrames,
            CombatFrameOutcome.PerfectDodge
        );
    }

    public void SetOutcome(CombatFrameOutcome outcome)
    {
        lastOutcome = outcome;
        OnOutcome?.Invoke(this, outcome);
        system?.NotifyOutcome(this, outcome);
    }

    public CombatFrameTelemetry CreateTelemetry()
    {
        int startupRemaining = 0;
        int activeRemaining = 0;
        int recoveryRemaining = 0;
        if (currentAction != null)
        {
            if (localActionFrame < currentAction.StartupFrames)
            {
                startupRemaining =
                    currentAction.StartupFrames - localActionFrame;
                activeRemaining = currentAction.ActiveFrames;
                recoveryRemaining = currentAction.RecoveryFrames;
            }
            else if (localActionFrame <
                     currentAction.StartupFrames +
                     currentAction.ActiveFrames)
            {
                activeRemaining =
                    currentAction.StartupFrames +
                    currentAction.ActiveFrames -
                    localActionFrame;
                recoveryRemaining = currentAction.RecoveryFrames;
            }
            else
            {
                recoveryRemaining =
                    currentAction.TotalFrames - localActionFrame;
            }
        }

        return new CombatFrameTelemetry(
            currentGlobalFrame,
            CurrentActionId,
            CurrentPhase,
            localActionFrame,
            Mathf.Max(0, startupRemaining),
            Mathf.Max(0, activeRemaining),
            Mathf.Max(0, recoveryRemaining),
            hitstopRemaining,
            hitstunRemaining,
            blockstunRemaining,
            IsInvulnerable,
            IsPerfectDodgeWindow,
            IsInterruptible,
            commandBuffer.BufferedAction,
            commandBuffer.RemainingFrames(currentGlobalFrame),
            lastOutcome,
            destinationValidated,
            dodgeInterrupted
        );
    }

    public void Reset(bool enableCombat)
    {
        combatEnabled = enableCombat;
        CancelAll(true);
        currentGlobalFrame = 0;
        lastPermutationToken = long.MinValue;
        lastOutcome = CombatFrameOutcome.None;
        currentAction = null;
        CurrentPhase = CombatActionPhase.Idle;
        owner?.FrameApplyDrivenState(
            FighterCombatState.Idle,
            FighterStunReason.None,
            0f
        );
    }

    public void CancelAll(bool restoreNeutralPose)
    {
        if (hasDodgeTransaction)
        {
            spatial?.CancelDodge(dodgeTransaction);
            hasDodgeTransaction = false;
            dodgeTransaction = default;
        }

        currentAction = null;
        currentCommand = default;
        localActionFrame = -1;
        actionStartGlobalFrame = 0;
        hitstopRemaining = 0;
        hitstunRemaining = 0;
        blockstunRemaining = 0;
        guardBreakRemaining = 0;
        riposteRemaining = 0;
        guardLocalFrame = 0;
        simpleGuardRemaining = 0;
        guardStartupCostPaid = 0f;
        rechargeLocalFrame = 0;
        hitRegistered = false;
        guardRequested = false;
        guarding = false;
        rechargeRequested = false;
        recharging = false;
        destinationValidated = false;
        dodgeInterrupted = false;
        pendingAutoFace = false;
        commandBuffer.Clear();
        owner?.StopSpatialMovement();
        if (restoreNeutralPose)
            owner?.FrameRestoreNeutralPose();
    }

    private void BuildDefinitions()
    {
        definitions[CombatActionId.AttackA] =
            settings.CreateAttackA();
        definitions[CombatActionId.AttackB] =
            settings.CreateAttackB();
        definitions[CombatActionId.AttackC] =
            settings.CreateAttackC();

        CombatFrameWindow invulnerability =
            settings.DodgeInvulnerabilityWindow;
        CombatFrameWindow perfect =
            settings.PerfectDodgeWindow;
        int dodgeStartup = invulnerability.StartInclusive;
        int dodgeActive =
            invulnerability.EndExclusive - dodgeStartup;
        int dodgeRecovery =
            settings.DodgeTotalFrames -
            invulnerability.EndExclusive;
        foreach (CombatActionId dodgeId in new[]
                 {
                     CombatActionId.DodgeLeft,
                     CombatActionId.DodgeRight,
                     CombatActionId.DodgeForward,
                     CombatActionId.DodgeBackward
                 })
        {
            definitions[dodgeId] = new CombatActionDefinition(
                dodgeId,
                dodgeStartup,
                dodgeActive,
                dodgeRecovery,
                0f,
                0,
                0,
                0,
                0,
                0f,
                settings.DodgeStaminaCost,
                0f,
                float.MaxValue,
                360f,
                false,
                false,
                false,
                invulnerability,
                perfect
            );
        }

        definitions[CombatActionId.Permutation] =
            new CombatActionDefinition(
                CombatActionId.Permutation,
                settings.PermutationStartupFrames,
                settings.PermutationActiveFrames,
                settings.PermutationRecoveryFrames,
                0f,
                0,
                0,
                0,
                0,
                0f,
                0f,
                0f,
                float.MaxValue,
                360f,
                false,
                false,
                false,
                new CombatFrameWindow(2, 8),
                default
            );
    }

    private CombatActionResult ValidateImmediateCostAndSpace(
        CombatFrameCommand command)
    {
        bool isFacingPivot =
            IsDodge(command.ActionId) &&
            spatial != null &&
            spatial.IsFacingPivot(
                owner,
                ToDodgeDirection(command.ActionId)
            );
        float cost = isFacingPivot
            ? 0f
            : ResolveStaminaCost(command.ActionId);
        if (stats == null ||
            stats.CurrentStamina + Mathf.Epsilon < cost)
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.NotEnoughStamina;
        }

        if (command.ActionId == CombatActionId.Guard &&
            !CanGuard())
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.Unavailable;
        }

        if (IsDodge(command.ActionId) &&
            spatial != null &&
            !spatial.CanDodge(
                owner,
                ToDodgeDirection(command.ActionId)))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return CombatActionResult.Unavailable;
        }

        return CombatActionResult.Started;
    }

    private bool CanStartCommandNow(CombatFrameCommand command)
    {
        return currentAction == null &&
               !guarding &&
               !recharging &&
               hitstopRemaining <= 0 &&
               hitstunRemaining <= 0 &&
               blockstunRemaining <= 0 &&
               guardBreakRemaining <= 0 &&
               !IsDead;
    }

    private void StartCommand(CombatFrameCommand command)
    {
        if (!CanStartCommandNow(command))
        {
            commandBuffer.Store(command);
            return;
        }

        CombatActionResult validation =
            ValidateImmediateCostAndSpace(command);
        if (validation != CombatActionResult.Started)
            return;

        if (command.ActionId == CombatActionId.Guard)
        {
            StartGuard();
            return;
        }

        if (command.ActionId == CombatActionId.Recharge)
        {
            StartRecharge();
            return;
        }

        if (IsDodge(command.ActionId))
        {
            DodgeDirection direction =
                ToDodgeDirection(command.ActionId);
            if (spatial != null &&
                spatial.IsFacingPivot(owner, direction))
            {
                StartFacingPivot(direction);
                return;
            }

            StartDodge(command);
            return;
        }

        if (command.ActionId == CombatActionId.Permutation)
        {
            StartPermutation(command);
            return;
        }

        StartResolvedAction(command);
    }

    private void StartResolvedAction(CombatFrameCommand command)
    {
        CombatActionDefinition definition = GetDefinition(command.ActionId);
        if (definition == null)
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        ResolvedCombatAction resolved =
            ResolveSnapshot(definition);
        if (!stats.SpendStamina(resolved.StaminaCost))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        owner?.StopSpatialMovement();
        spatial?.NotifySignificantAction();
        currentCommand = command;
        currentAction = resolved;
        localActionFrame = 0;
        actionStartGlobalFrame = currentGlobalFrame;
        hitRegistered = false;
        destinationValidated = false;
        dodgeInterrupted = false;
        SetOutcome(CombatFrameOutcome.Started);
        UpdateActionPhase();
        ApplyStateToOwner();
    }

    private void StartFacingPivot(DodgeDirection direction)
    {
        owner?.StopSpatialMovement();
        if (spatial == null ||
            !spatial.TryApplyFacingPivot(owner, direction))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        SetOutcome(CombatFrameOutcome.Started);
        CurrentPhase = CombatActionPhase.Idle;
        ApplyStateToOwner();
    }

    private void StartDodge(CombatFrameCommand command)
    {
        DodgeDirection direction =
            ToDodgeDirection(command.ActionId);
        SpatialDodgeTransaction transaction = default;
        if (spatial != null &&
            !spatial.TryPrepareDodge(
                owner,
                direction,
                out transaction))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        CombatActionDefinition definition = GetDefinition(command.ActionId);
        ResolvedCombatAction resolved = ResolveSnapshot(definition);
        if (!stats.SpendStamina(resolved.StaminaCost))
        {
            if (transaction.IsValid)
                spatial?.CancelDodge(transaction);
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        owner?.StopSpatialMovement();
        spatial?.NotifySignificantAction();
        currentCommand = command;
        currentAction = resolved;
        localActionFrame = 0;
        actionStartGlobalFrame = currentGlobalFrame;
        currentDodgeDirection = direction;
        dodgeTransaction = transaction;
        hasDodgeTransaction = transaction.IsValid;
        destinationValidated = false;
        dodgeInterrupted = false;
        hitRegistered = false;
        SetOutcome(CombatFrameOutcome.Started);
        UpdateActionPhase();
        ApplyStateToOwner();
    }

    private void StartPermutation(CombatFrameCommand command)
    {
        if (command.Token <= 0 ||
            command.Token <= lastPermutationToken ||
            spatial == null ||
            !spatial.CanApplyPermutation)
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        float cost = rules.ResolvePermutationStaminaCost();
        if (!stats.SpendStamina(cost))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        lastPermutationToken = command.Token;
        currentCommand = command;
        currentAction = ResolveSnapshot(
            GetDefinition(CombatActionId.Permutation)
        );
        localActionFrame = 0;
        actionStartGlobalFrame = currentGlobalFrame;
        hitRegistered = false;
        destinationValidated = false;
        dodgeInterrupted = false;
        owner?.StopSpatialMovement();
        SetOutcome(CombatFrameOutcome.Started);
        UpdateActionPhase();
        ApplyStateToOwner();
    }

    private void StartGuard()
    {
        float startupCost = guardRequested
            ? 0f
            : settings.SimpleGuardStaminaCost;
        if (!stats.SpendStamina(startupCost))
        {
            SetOutcome(CombatFrameOutcome.Rejected);
            return;
        }

        owner?.StopSpatialMovement();
        guardStartupCostPaid = startupCost;
        spatial?.NotifySignificantAction();
        guarding = true;
        recharging = false;
        guardLocalFrame = 0;
        simpleGuardRemaining = guardRequested
            ? int.MaxValue
            : settings.SimpleGuardFrames;
        CurrentPhase = CombatActionPhase.Parrying;
        SetOutcome(CombatFrameOutcome.Started);
        ApplyStateToOwner();
    }

    private void StopGuard()
    {
        guarding = false;
        guardLocalFrame = 0;
        simpleGuardRemaining = 0;
        guardStartupCostPaid = 0f;
        CurrentPhase = CombatActionPhase.Idle;
        ApplyStateToOwner();
    }

    private void TickGuard()
    {
        if (!guardRequested &&
            simpleGuardRemaining <= 0)
        {
            StopGuard();
            return;
        }

        CurrentPhase = IsParryActive
            ? CombatActionPhase.Parrying
            : CombatActionPhase.Guarding;
    }

    private void StartRecharge()
    {
        owner?.StopSpatialMovement();
        guarding = false;
        recharging = true;
        rechargeLocalFrame = 0;
        CurrentPhase = CombatActionPhase.Recharging;
        SetOutcome(CombatFrameOutcome.Started);
        ApplyStateToOwner();
    }

    private void StopRecharge()
    {
        recharging = false;
        rechargeLocalFrame = 0;
        CurrentPhase = CombatActionPhase.Idle;
        ApplyStateToOwner();
    }

    private void TickRecharge()
    {
        if (!rechargeRequested)
        {
            StopRecharge();
            return;
        }

        CurrentPhase = CombatActionPhase.Recharging;
        if (rechargeLocalFrame < settings.RechargeStartupFrames)
            return;

        int activeRechargeFrame =
            rechargeLocalFrame - settings.RechargeStartupFrames;
        if (activeRechargeFrame %
            settings.RechargeTickIntervalFrames == 0)
        {
            stats.RecoverStaminaFromCharge(
                settings.RechargePerTick
            );
        }
    }

    private void UpdateActionPhase()
    {
        if (currentAction == null)
        {
            CurrentPhase = CombatActionPhase.Idle;
            return;
        }

        if (IsDodge(currentAction.Id))
        {
            CurrentPhase = CombatActionPhase.Dodging;
            return;
        }

        if (currentAction.Id == CombatActionId.Permutation)
        {
            CurrentPhase = CombatActionPhase.Permutation;
            return;
        }

        if (currentAction.BaseDefinition.IsStartup(localActionFrame))
            CurrentPhase = CombatActionPhase.Startup;
        else if (currentAction.BaseDefinition.IsActive(localActionFrame))
            CurrentPhase = CombatActionPhase.Active;
        else
            CurrentPhase = CombatActionPhase.Recovery;
    }

    private void UpdateActionVisuals()
    {
        if (currentAction == null)
            return;

        if (IsAttack(currentAction.Id))
        {
            float lunge = ResolveAttackLungeProgress();
            owner?.FrameApplyAttackLunge(lunge);
            return;
        }

        if (IsDodge(currentAction.Id))
        {
            UpdateDodgeMovement();
            return;
        }

        if (currentAction.Id == CombatActionId.Permutation &&
            localActionFrame ==
            currentAction.StartupFrames)
        {
            if (spatial != null &&
                spatial.ApplyPermutation(owner))
            {
                destinationValidated = true;
                spatial.NotifySignificantAction();
            }
        }
    }

    private float ResolveAttackLungeProgress()
    {
        if (currentAction == null)
            return 0f;

        if (currentAction.BaseDefinition.IsStartup(localActionFrame))
        {
            return currentAction.StartupFrames <= 0
                ? 1f
                : Mathf.Clamp01(
                    (localActionFrame + 1f) /
                    currentAction.StartupFrames
                );
        }

        if (currentAction.BaseDefinition.IsActive(localActionFrame))
            return 1f;

        int recoveryFrame =
            localActionFrame -
            currentAction.StartupFrames -
            currentAction.ActiveFrames;
        return currentAction.RecoveryFrames <= 0
            ? 0f
            : 1f - Mathf.Clamp01(
                (recoveryFrame + 1f) /
                currentAction.RecoveryFrames
            );
    }

    private void UpdateDodgeMovement()
    {
        if (!hasDodgeTransaction || spatial == null)
            return;

        const int destinationValidationFrame = 19;
        if (localActionFrame < destinationValidationFrame)
        {
            float progress = Mathf.Clamp01(
                (localActionFrame + 1f) /
                destinationValidationFrame
            );
            spatial.PreviewPreparedDodge(
                dodgeTransaction.Id,
                progress
            );
            return;
        }

        if (!destinationValidated)
        {
            spatial.PreviewPreparedDodge(
                dodgeTransaction.Id,
                1f
            );
            destinationValidated =
                spatial.CommitDodge(dodgeTransaction);
            hasDodgeTransaction = false;
            dodgeTransaction = default;
        }
    }

    private void FinishCurrentAction()
    {
        bool wasAttack =
            currentAction != null && IsAttack(currentAction.Id);
        if (wasAttack && !hitRegistered)
        {
            SetOutcome(CombatFrameOutcome.Whiff);
            RaiseWhiffFeedback();
        }

        if (hasDodgeTransaction)
        {
            spatial?.CommitDodge(dodgeTransaction);
            hasDodgeTransaction = false;
            dodgeTransaction = default;
            destinationValidated = true;
        }

        currentAction = null;
        currentCommand = default;
        localActionFrame = -1;
        hitRegistered = false;
        owner?.FrameRestoreNeutralPose();
        CurrentPhase = CombatActionPhase.Idle;
        ApplyStateToOwner();
    }

    private void RaiseWhiffFeedback()
    {
        if (owner == null || target == null)
            return;

        RelativeOrientation orientation = spatial != null
            ? spatial.GetAttackOrientation(owner, target)
            : RelativeOrientation.Face;
        float multiplier = spatial != null
            ? spatial.GetDamageMultiplier(owner, target)
            : 1f;
        owner.FrameRaiseImpact(
            new CombatImpact(
                owner,
                target,
                CombatHitResult.Missed,
                currentGlobalFrame /
                (float)settings.FramesPerSecond,
                orientation,
                multiplier,
                0f
            )
        );
    }

    private void InterruptCurrentAction(CombatFrameOutcome outcome)
    {
        bool interruptedDodgeBeforeInvulnerability = false;
        if (IsDodging && hasDodgeTransaction)
        {
            if (localActionFrame <
                settings.DodgeInvulnerabilityWindow.StartInclusive)
            {
                spatial?.InterruptDodgeAtCurrentPose(
                    dodgeTransaction
                );
                dodgeInterrupted = true;
                destinationValidated = false;
                interruptedDodgeBeforeInvulnerability = true;
            }
            else if (!destinationValidated)
            {
                spatial?.CancelDodge(dodgeTransaction);
            }
            hasDodgeTransaction = false;
            dodgeTransaction = default;
        }

        currentAction = null;
        currentCommand = default;
        localActionFrame = -1;
        hitRegistered = false;
        guarding = false;
        recharging = false;
        guardRequested = false;
        rechargeRequested = false;
        owner?.FrameRestoreNeutralPose();
        SetOutcome(
            interruptedDodgeBeforeInvulnerability
                ? CombatFrameOutcome.InterruptedDodge
                : outcome
        );
    }

    private void TickGuardBreak()
    {
        int total = settings.GuardBreakFrames;
        int elapsed = total - guardBreakRemaining;
        float normalized = total <= 0
            ? 1f
            : Mathf.Clamp01((elapsed + 1f) / total);
        AnimationCurve curve = rules.StunRecoveryCurve;
        float progress = curve != null
            ? Mathf.Clamp01(curve.Evaluate(normalized))
            : normalized;
        stats.SetStamina(
            settings.GuardBreakRecoveryStamina * progress
        );
        guardBreakRemaining--;
        CurrentPhase = CombatActionPhase.GuardBrokenStun;
        if (guardBreakRemaining <= 0)
        {
            stats.SetStamina(
                settings.GuardBreakRecoveryStamina
            );
            CurrentPhase = CombatActionPhase.Idle;
        }
    }

    private bool CanGuard()
    {
        if (spatial == null || target == null)
            return true;

        return spatial.GetAttackOrientation(target, owner) ==
               RelativeOrientation.Face;
    }

    private float ResolveStaminaCost(CombatActionId id)
    {
        if (id == CombatActionId.Guard)
        {
            return guardRequested
                ? 0f
                : settings.SimpleGuardStaminaCost;
        }

        if (id == CombatActionId.Recharge)
        {
            return 0f;
        }

        if (id == CombatActionId.Permutation)
            return rules.ResolvePermutationStaminaCost();

        CombatActionDefinition definition = GetDefinition(id);
        return definition != null ? definition.StaminaCost : 0f;
    }

    private ResolvedCombatAction ResolveSnapshot(
        CombatActionDefinition definition)
    {
        ResolvedCombatAction resolved =
            new(definition);
        for (int i = 0; i < modifiers.Count; i++)
        {
            ResolvedCombatAction modified =
                modifiers[i].Apply(owner, resolved);
            if (modified != null)
                resolved = modified;
        }
        return resolved;
    }

    private void ApplyStateToOwner()
    {
        if (owner == null)
            return;

        FighterCombatState state = CurrentPhase switch
        {
            CombatActionPhase.Startup =>
                FighterCombatState.AttackStartup,
            CombatActionPhase.Active =>
                FighterCombatState.Attacking,
            CombatActionPhase.Recovery =>
                FighterCombatState.Recovering,
            CombatActionPhase.Guarding or
            CombatActionPhase.Parrying =>
                FighterCombatState.Defending,
            CombatActionPhase.Dodging =>
                FighterCombatState.Dodging,
            CombatActionPhase.Recharging =>
                FighterCombatState.Charging,
            CombatActionPhase.Hitstun or
            CombatActionPhase.Blockstun or
            CombatActionPhase.GuardBrokenStun or
            CombatActionPhase.Hitstop =>
                FighterCombatState.Stunned,
            CombatActionPhase.Dead =>
                FighterCombatState.Dead,
            _ => FighterCombatState.Idle
        };

        FighterStunReason stunReason =
            guardBreakRemaining > 0
                ? FighterStunReason.GuardBreak
                : hitstunRemaining > 0
                    ? FighterStunReason.Countered
                    : FighterStunReason.None;
        float remainingSeconds =
            Mathf.Max(
                guardBreakRemaining,
                hitstunRemaining
            ) /
            (float)settings.FramesPerSecond;
        owner.FrameApplyDrivenState(
            state,
            stunReason,
            remainingSeconds
        );
    }

    private static bool IsKnownCommand(CombatActionId id)
    {
        return id != CombatActionId.None;
    }

    public static bool IsAttack(CombatActionId id)
    {
        return id is
            CombatActionId.AttackA or
            CombatActionId.AttackB or
            CombatActionId.AttackC;
    }

    public static bool IsDodge(CombatActionId id)
    {
        return id is
            CombatActionId.DodgeLeft or
            CombatActionId.DodgeRight or
            CombatActionId.DodgeForward or
            CombatActionId.DodgeBackward;
    }

    private static DodgeDirection ToDodgeDirection(CombatActionId id)
    {
        return id switch
        {
            CombatActionId.DodgeLeft => DodgeDirection.Left,
            CombatActionId.DodgeRight => DodgeDirection.Right,
            CombatActionId.DodgeForward => DodgeDirection.Forward,
            CombatActionId.DodgeBackward => DodgeDirection.Backward,
            _ => DodgeDirection.Left
        };
    }
}
