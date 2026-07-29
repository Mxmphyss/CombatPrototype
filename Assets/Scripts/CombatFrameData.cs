using System;
using UnityEngine;

public enum CombatActionId
{
    None,
    AttackA,
    AttackB,
    AttackC,
    Guard,
    DodgeLeft,
    DodgeRight,
    DodgeForward,
    DodgeBackward,
    Permutation,
    Recharge
}

public enum CombatActionPhase
{
    Idle,
    Startup,
    Active,
    Recovery,
    Hitstop,
    Hitstun,
    Blockstun,
    Guarding,
    Parrying,
    Dodging,
    Recharging,
    Permutation,
    GuardBrokenStun,
    Dead
}

public enum CombatFrameOutcome
{
    None,
    Hit,
    CounterHit,
    Punish,
    Block,
    GuardBreak,
    Parry,
    Dodge,
    PerfectDodge,
    Trade,
    Whiff,
    InterruptedDodge,
    Buffered,
    Replaced,
    Expired,
    Rejected,
    Started
}

public enum CombatCommandBufferStatus
{
    None,
    Buffered,
    Replaced,
    Expired,
    Rejected,
    Started
}

public readonly struct CombatFrameWindow
{
    public int StartInclusive { get; }
    public int EndExclusive { get; }
    public int Length => Mathf.Max(0, EndExclusive - StartInclusive);

    public CombatFrameWindow(int startInclusive, int endExclusive)
    {
        StartInclusive = Mathf.Max(0, startInclusive);
        EndExclusive = Mathf.Max(StartInclusive, endExclusive);
    }

    public bool Contains(int localFrame)
    {
        return localFrame >= StartInclusive &&
               localFrame < EndExclusive;
    }

    public override string ToString()
    {
        return $"[{StartInclusive}, {EndExclusive})";
    }
}

[Serializable]
public sealed class CombatFrameDataSettings
{
    [Header("Horloge et buffer")]
    [Min(1)]
    [SerializeField] private int combatFramesPerSecond = 60;
    [Min(1)]
    [SerializeField] private int inputBufferFrames = 6;
    [Min(1)]
    [SerializeField] private int maxCatchUpTicks = 8;

    [Header("Attaque A")]
    [SerializeField] private int attackAStartup = 7;
    [SerializeField] private int attackAActive = 3;
    [SerializeField] private int attackARecovery = 12;
    [SerializeField] private int attackAHitstop = 3;
    [SerializeField] private int attackAHitstun = 16;
    [SerializeField] private int attackABlockstun = 9;
    [SerializeField] private int attackACounterBonus = 3;
    [SerializeField] private float attackADamage = 20f;
    [SerializeField] private float attackAStaminaCost = 10f;
    [SerializeField] private float attackAMaxRange = 6.75f;

    [Header("Attaque B")]
    [SerializeField] private int attackBStartup = 11;
    [SerializeField] private int attackBActive = 4;
    [SerializeField] private int attackBRecovery = 17;
    [SerializeField] private int attackBHitstop = 4;
    [SerializeField] private int attackBHitstun = 22;
    [SerializeField] private int attackBBlockstun = 13;
    [SerializeField] private int attackBCounterBonus = 3;
    [SerializeField] private float attackBDamage = 30f;
    [SerializeField] private float attackBStaminaCost = 18f;
    [SerializeField] private float attackBMaxRange = 9.75f;

    [Header("Attaque C")]
    [SerializeField] private int attackCStartup = 18;
    [SerializeField] private int attackCActive = 5;
    [SerializeField] private int attackCRecovery = 26;
    [SerializeField] private int attackCHitstop = 6;
    [SerializeField] private int attackCHitstun = 34;
    [SerializeField] private int attackCBlockstun = 18;
    [SerializeField] private int attackCCounterBonus = 4;
    [SerializeField] private float attackCDamage = 45f;
    [SerializeField] private float attackCStaminaCost = 30f;
    [SerializeField] private float attackCMaxRange = 12.75f;

    [Header("Defense")]
    [SerializeField] private int simpleGuardFrames = 48;
    [SerializeField] private float simpleGuardStaminaCost = 10f;
    [SerializeField] private int parryActiveFrames = 7;
    [SerializeField] private float parryStaminaRefundRatio = 0.5f;
    [SerializeField] private int riposteWindowFrames = 30;
    [SerializeField] private int guardBreakFrames = 240;
    [SerializeField] private float guardStaminaDamage = 15f;
    [SerializeField] private float guardBreakRecoveryStamina = 15f;

    [Header("Esquive")]
    [SerializeField] private int dodgeTotalFrames = 26;
    [SerializeField] private int dodgeInvulnerabilityStart = 5;
    [SerializeField] private int dodgeInvulnerabilityEnd = 19;
    [SerializeField] private int perfectDodgeStart = 9;
    [SerializeField] private int perfectDodgeEnd = 15;
    [SerializeField] private float dodgeStaminaCost = 20f;

    [Header("Permutation et recharge")]
    [SerializeField] private int permutationStartupFrames = 3;
    [SerializeField] private int permutationActiveFrames = 5;
    [SerializeField] private int permutationRecoveryFrames = 6;
    [SerializeField] private int rechargeStartupFrames = 18;
    [SerializeField] private int rechargeTickIntervalFrames = 6;
    [SerializeField] private float rechargePerTick = 2.5f;

    [Header("Orientation")]
    [SerializeField] private int flankAutoFaceFrames = 180;
    [Range(1f, 360f)]
    [SerializeField] private float attackConeDegrees = 100f;

    public int FramesPerSecond => Mathf.Max(1, combatFramesPerSecond);
    public int InputBufferFrames => Mathf.Max(1, inputBufferFrames);
    public int MaxCatchUpTicks => Mathf.Max(1, maxCatchUpTicks);
    public int SimpleGuardFrames => Mathf.Max(1, simpleGuardFrames);
    public float SimpleGuardStaminaCost =>
        Mathf.Max(0f, simpleGuardStaminaCost);
    public int ParryActiveFrames => Mathf.Max(0, parryActiveFrames);
    public float ParryStaminaRefundRatio =>
        Mathf.Clamp01(parryStaminaRefundRatio);
    public int RiposteWindowFrames => Mathf.Max(0, riposteWindowFrames);
    public int GuardBreakFrames => Mathf.Max(1, guardBreakFrames);
    public float GuardStaminaDamage => Mathf.Max(0f, guardStaminaDamage);
    public float GuardBreakRecoveryStamina =>
        Mathf.Max(0f, guardBreakRecoveryStamina);
    public int DodgeTotalFrames => Mathf.Max(1, dodgeTotalFrames);
    public CombatFrameWindow DodgeInvulnerabilityWindow =>
        new(
            dodgeInvulnerabilityStart,
            Mathf.Min(dodgeTotalFrames, dodgeInvulnerabilityEnd)
        );
    public CombatFrameWindow PerfectDodgeWindow =>
        new(
            perfectDodgeStart,
            Mathf.Min(dodgeTotalFrames, perfectDodgeEnd)
        );
    public float DodgeStaminaCost => Mathf.Max(0f, dodgeStaminaCost);
    public int PermutationStartupFrames =>
        Mathf.Max(0, permutationStartupFrames);
    public int PermutationActiveFrames =>
        Mathf.Max(1, permutationActiveFrames);
    public int PermutationRecoveryFrames =>
        Mathf.Max(0, permutationRecoveryFrames);
    public int RechargeStartupFrames =>
        Mathf.Max(0, rechargeStartupFrames);
    public int RechargeTickIntervalFrames =>
        Mathf.Max(1, rechargeTickIntervalFrames);
    public float RechargePerTick => Mathf.Max(0f, rechargePerTick);
    public int FlankAutoFaceFrames => Mathf.Max(0, flankAutoFaceFrames);
    public float AttackConeDegrees =>
        Mathf.Clamp(attackConeDegrees, 1f, 360f);

    public CombatActionDefinition CreateAttackA()
    {
        return CreateAttack(
            CombatActionId.AttackA,
            attackAStartup,
            attackAActive,
            attackARecovery,
            attackAHitstop,
            attackAHitstun,
            attackABlockstun,
            attackACounterBonus,
            attackADamage,
            attackAStaminaCost,
            attackAMaxRange
        );
    }

    public CombatActionDefinition CreateAttackB()
    {
        return CreateAttack(
            CombatActionId.AttackB,
            attackBStartup,
            attackBActive,
            attackBRecovery,
            attackBHitstop,
            attackBHitstun,
            attackBBlockstun,
            attackBCounterBonus,
            attackBDamage,
            attackBStaminaCost,
            attackBMaxRange
        );
    }

    public CombatActionDefinition CreateAttackC()
    {
        return CreateAttack(
            CombatActionId.AttackC,
            attackCStartup,
            attackCActive,
            attackCRecovery,
            attackCHitstop,
            attackCHitstun,
            attackCBlockstun,
            attackCCounterBonus,
            attackCDamage,
            attackCStaminaCost,
            attackCMaxRange
        );
    }

    private CombatActionDefinition CreateAttack(
        CombatActionId id,
        int startup,
        int active,
        int recovery,
        int hitstop,
        int hitstun,
        int blockstun,
        int counterBonus,
        float damage,
        float staminaCost,
        float maximumRange)
    {
        return new CombatActionDefinition(
            id,
            startup,
            active,
            recovery,
            damage,
            hitstop,
            hitstun,
            blockstun,
            counterBonus,
            GuardStaminaDamage,
            staminaCost,
            0f,
            Mathf.Max(0f, maximumRange),
            AttackConeDegrees,
            true,
            true,
            true,
            default,
            default
        );
    }
}

public sealed class CombatActionDefinition
{
    public CombatActionId Id { get; }
    public int StartupFrames { get; }
    public int ActiveFrames { get; }
    public int RecoveryFrames { get; }
    public int TotalFrames =>
        StartupFrames + ActiveFrames + RecoveryFrames;
    public int EarliestActiveFrame => StartupFrames;
    public int LatestActiveFrame =>
        StartupFrames + Mathf.Max(0, ActiveFrames - 1);
    public float Damage { get; }
    public int HitstopFrames { get; }
    public int HitstunFrames { get; }
    public int BlockstunFrames { get; }
    public int CounterHitBonusFrames { get; }
    public float GuardStaminaDamage { get; }
    public float StaminaCost { get; }
    public float MinimumRange { get; }
    public float MaximumRange { get; }
    public float AttackConeDegrees { get; }
    public bool Blockable { get; }
    public bool Parryable { get; }
    public bool Dodgeable { get; }
    public CombatFrameWindow InvulnerabilityWindow { get; }
    public CombatFrameWindow PerfectDodgeWindow { get; }
    public int AdvantageOnHitFirstActive =>
        CalculateAdvantage(HitstunFrames, EarliestActiveFrame);
    public int AdvantageOnHitLastActive =>
        CalculateAdvantage(HitstunFrames, LatestActiveFrame);
    public int AdvantageOnBlockFirstActive =>
        CalculateAdvantage(BlockstunFrames, EarliestActiveFrame);
    public int AdvantageOnBlockLastActive =>
        CalculateAdvantage(BlockstunFrames, LatestActiveFrame);

    public CombatActionDefinition(
        CombatActionId id,
        int startupFrames,
        int activeFrames,
        int recoveryFrames,
        float damage,
        int hitstopFrames,
        int hitstunFrames,
        int blockstunFrames,
        int counterHitBonusFrames,
        float guardStaminaDamage,
        float staminaCost,
        float minimumRange,
        float maximumRange,
        float attackConeDegrees,
        bool blockable,
        bool parryable,
        bool dodgeable,
        CombatFrameWindow invulnerabilityWindow,
        CombatFrameWindow perfectDodgeWindow)
    {
        Id = id;
        StartupFrames = Mathf.Max(0, startupFrames);
        ActiveFrames = Mathf.Max(0, activeFrames);
        RecoveryFrames = Mathf.Max(0, recoveryFrames);
        Damage = Mathf.Max(0f, damage);
        HitstopFrames = Mathf.Max(0, hitstopFrames);
        HitstunFrames = Mathf.Max(0, hitstunFrames);
        BlockstunFrames = Mathf.Max(0, blockstunFrames);
        CounterHitBonusFrames = Mathf.Max(0, counterHitBonusFrames);
        GuardStaminaDamage = Mathf.Max(0f, guardStaminaDamage);
        StaminaCost = Mathf.Max(0f, staminaCost);
        MinimumRange = Mathf.Max(0f, minimumRange);
        MaximumRange = Mathf.Max(MinimumRange, maximumRange);
        AttackConeDegrees =
            Mathf.Clamp(attackConeDegrees, 1f, 360f);
        Blockable = blockable;
        Parryable = parryable;
        Dodgeable = dodgeable;
        InvulnerabilityWindow = invulnerabilityWindow;
        PerfectDodgeWindow = perfectDodgeWindow;
    }

    public bool IsStartup(int localFrame)
    {
        return localFrame >= 0 && localFrame < StartupFrames;
    }

    public bool IsActive(int localFrame)
    {
        return localFrame >= StartupFrames &&
               localFrame < StartupFrames + ActiveFrames;
    }

    public bool IsRecovery(int localFrame)
    {
        return localFrame >= StartupFrames + ActiveFrames &&
               localFrame < TotalFrames;
    }

    public int CalculateAdvantage(
        int defenderStunFrames,
        int impactLocalFrame)
    {
        int remainingAttackerFrames =
            Mathf.Max(0, TotalFrames - impactLocalFrame - 1);
        return defenderStunFrames - remainingAttackerFrames;
    }
}

public sealed class ResolvedCombatAction
{
    public CombatActionDefinition BaseDefinition { get; }
    public CombatActionId Id => BaseDefinition.Id;
    public int StartupFrames => BaseDefinition.StartupFrames;
    public int ActiveFrames => BaseDefinition.ActiveFrames;
    public int RecoveryFrames => BaseDefinition.RecoveryFrames;
    public int TotalFrames => BaseDefinition.TotalFrames;
    public float Damage { get; }
    public int HitstopFrames { get; }
    public int HitstunFrames { get; }
    public int BlockstunFrames { get; }
    public int CounterHitBonusFrames { get; }
    public float GuardStaminaDamage { get; }
    public float StaminaCost { get; }
    public float MinimumRange { get; }
    public float MaximumRange { get; }
    public float AttackConeDegrees { get; }
    public CombatFrameWindow InvulnerabilityWindow =>
        BaseDefinition.InvulnerabilityWindow;
    public CombatFrameWindow PerfectDodgeWindow =>
        BaseDefinition.PerfectDodgeWindow;

    public ResolvedCombatAction(
        CombatActionDefinition definition,
        float damageMultiplier = 1f,
        float staminaCostMultiplier = 1f,
        int hitstunAdditiveFrames = 0)
    {
        BaseDefinition = definition ??
            throw new ArgumentNullException(nameof(definition));
        Damage = Mathf.Max(0f, definition.Damage * damageMultiplier);
        HitstopFrames = Mathf.Max(0, definition.HitstopFrames);
        HitstunFrames = Mathf.Max(
            0,
            definition.HitstunFrames + hitstunAdditiveFrames
        );
        BlockstunFrames = Mathf.Max(0, definition.BlockstunFrames);
        CounterHitBonusFrames =
            Mathf.Max(0, definition.CounterHitBonusFrames);
        GuardStaminaDamage =
            Mathf.Max(0f, definition.GuardStaminaDamage);
        StaminaCost = Mathf.Max(
            0f,
            definition.StaminaCost * staminaCostMultiplier
        );
        MinimumRange = Mathf.Max(0f, definition.MinimumRange);
        MaximumRange = Mathf.Max(MinimumRange, definition.MaximumRange);
        AttackConeDegrees = definition.AttackConeDegrees;
    }
}

public interface ICombatActionModifier
{
    int Priority { get; }

    ResolvedCombatAction Apply(
        FighterCombat fighter,
        ResolvedCombatAction current);
}

public readonly struct CombatFrameCommand
{
    public CombatActionId ActionId { get; }
    public int SubmittedFrame { get; }
    public long Token { get; }

    public CombatFrameCommand(
        CombatActionId actionId,
        int submittedFrame,
        long token = 0)
    {
        ActionId = actionId;
        SubmittedFrame = Mathf.Max(0, submittedFrame);
        Token = token;
    }

    public bool IsValid => ActionId != CombatActionId.None;
}

public readonly struct CombatFrameTelemetry
{
    public int GlobalFrame { get; }
    public CombatActionId CurrentAction { get; }
    public CombatActionPhase CurrentPhase { get; }
    public int LocalActionFrame { get; }
    public int StartupRemaining { get; }
    public int ActiveRemaining { get; }
    public int RecoveryRemaining { get; }
    public int HitstopRemaining { get; }
    public int HitstunRemaining { get; }
    public int BlockstunRemaining { get; }
    public bool Invulnerable { get; }
    public bool PerfectDodgeWindow { get; }
    public bool Interruptible { get; }
    public CombatActionId BufferedCommand { get; }
    public int BufferRemainingFrames { get; }
    public CombatFrameOutcome LastOutcome { get; }
    public bool DestinationValidated { get; }
    public bool DodgeInterrupted { get; }

    public CombatFrameTelemetry(
        int globalFrame,
        CombatActionId currentAction,
        CombatActionPhase currentPhase,
        int localActionFrame,
        int startupRemaining,
        int activeRemaining,
        int recoveryRemaining,
        int hitstopRemaining,
        int hitstunRemaining,
        int blockstunRemaining,
        bool invulnerable,
        bool perfectDodgeWindow,
        bool interruptible,
        CombatActionId bufferedCommand,
        int bufferRemainingFrames,
        CombatFrameOutcome lastOutcome,
        bool destinationValidated,
        bool dodgeInterrupted)
    {
        GlobalFrame = globalFrame;
        CurrentAction = currentAction;
        CurrentPhase = currentPhase;
        LocalActionFrame = localActionFrame;
        StartupRemaining = startupRemaining;
        ActiveRemaining = activeRemaining;
        RecoveryRemaining = recoveryRemaining;
        HitstopRemaining = hitstopRemaining;
        HitstunRemaining = hitstunRemaining;
        BlockstunRemaining = blockstunRemaining;
        Invulnerable = invulnerable;
        PerfectDodgeWindow = perfectDodgeWindow;
        Interruptible = interruptible;
        BufferedCommand = bufferedCommand;
        BufferRemainingFrames = bufferRemainingFrames;
        LastOutcome = lastOutcome;
        DestinationValidated = destinationValidated;
        DodgeInterrupted = dodgeInterrupted;
    }
}
