using System;
using UnityEngine;

public enum DistanceLevel
{
    CloseRange = 0,
    MidRange = 1,
    LongRange = 2
}

public enum RelativeOrientation
{
    Face = 0,
    LeftFlank = 1,
    RightFlank = 2,
    Back = 3
}

public enum SpatialMovementType
{
    None = 0,
    Advance = 1,
    Retreat = 2,
    StrafeLeft = 3,
    StrafeRight = 4
}

public enum DodgeDirection
{
    Left = 0,
    Right = 1,
    Forward = 2,
    Backward = 3
}

public enum CombatSpatialChangeReason
{
    Initialized = 0,
    ConfigurationChanged = 1,
    MovementInputChanged = 2,
    ContinuousMovement = 3,
    DodgePrepared = 4,
    DodgeCommitted = 5,
    DodgeCancelled = 6,
    AutoFaced = 7,
    PositionsPermuted = 8,
    TransientStateCancelled = 9,
    DuelReset = 10,
    SignificantAction = 11,
    CombatEnabledChanged = 12
}

[Serializable]
public sealed class CombatSpatialSettings
{
    [Header("Distance")]
    [Min(0.01f)]
    [SerializeField] private float minimumDistance = 3f;
    [Min(0.01f)]
    [SerializeField] private float closeRangeUpperBound = 4.25f;
    [Min(0.01f)]
    [SerializeField] private float midRangeDistance = 6f;
    [Min(0.01f)]
    [SerializeField] private float midRangeUpperBound = 7.25f;
    [Min(0.01f)]
    [SerializeField] private float maximumDistance = 9f;

    [Header("Mouvement")]
    [Min(0f)]
    [SerializeField] private float advanceSpeed = 2.5f;
    [Min(0f)]
    [SerializeField] private float retreatSpeed = 2f;
    [Min(0f)]
    [SerializeField]
    private float strafeSpeed = 1.5f;
    [Min(0.01f)]
    [SerializeField] private float rotationSpeed = 540f;

    [Header("Esquive")]
    [Range(0f, 180f)]
    [SerializeField] private float dodgeOrientationAngle = 90f;

    [Header("Orientation")]
    [SerializeField] private bool autoFaceFlanks = true;
    [Min(0f)]
    [SerializeField] private float flankAutoFaceDelay = 3f;

    [Header("Degats")]
    [Min(0f)]
    [SerializeField] private float faceDamageMultiplier = 1f;
    [Min(0f)]
    [SerializeField] private float flankDamageMultiplier = 1.25f;
    [Min(0f)]
    [SerializeField] private float backDamageMultiplier = 2f;

    public float MinimumDistance
    {
        get => minimumDistance;
        set => minimumDistance = value;
    }

    public float CloseRangeUpperBound
    {
        get => closeRangeUpperBound;
        set => closeRangeUpperBound = value;
    }

    public float MidRangeUpperBound
    {
        get => midRangeUpperBound;
        set => midRangeUpperBound = value;
    }

    public float MidRangeDistance
    {
        get => midRangeDistance;
        set => midRangeDistance = value;
    }

    public float MaximumDistance
    {
        get => maximumDistance;
        set => maximumDistance = value;
    }

    public float GetDistance(DistanceLevel level)
    {
        return level switch
        {
            DistanceLevel.CloseRange => minimumDistance,
            DistanceLevel.LongRange => maximumDistance,
            _ => midRangeDistance
        };
    }

    public float AdvanceSpeed
    {
        get => advanceSpeed;
        set => advanceSpeed = value;
    }

    public float RetreatSpeed
    {
        get => retreatSpeed;
        set => retreatSpeed = value;
    }

    public float StrafeSpeed
    {
        get => strafeSpeed;
        set => strafeSpeed = value;
    }

    public float RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    public float DodgeOrientationAngle
    {
        get => dodgeOrientationAngle;
        set => dodgeOrientationAngle = value;
    }

    public bool AutoFaceFlanks
    {
        get => autoFaceFlanks;
        set => autoFaceFlanks = value;
    }

    public float FlankAutoFaceDelay
    {
        get => flankAutoFaceDelay;
        set => flankAutoFaceDelay = value;
    }

    public float FaceDamageMultiplier
    {
        get => faceDamageMultiplier;
        set => faceDamageMultiplier = value;
    }

    public float FlankDamageMultiplier
    {
        get => flankDamageMultiplier;
        set => flankDamageMultiplier = value;
    }

    public float BackDamageMultiplier
    {
        get => backDamageMultiplier;
        set => backDamageMultiplier = value;
    }

    public CombatSpatialSettings Copy()
    {
        return new CombatSpatialSettings
        {
            minimumDistance = minimumDistance,
            closeRangeUpperBound = closeRangeUpperBound,
            midRangeDistance = midRangeDistance,
            midRangeUpperBound = midRangeUpperBound,
            maximumDistance = maximumDistance,
            advanceSpeed = advanceSpeed,
            retreatSpeed = retreatSpeed,
            strafeSpeed = strafeSpeed,
            rotationSpeed = rotationSpeed,
            dodgeOrientationAngle = dodgeOrientationAngle,
            autoFaceFlanks = autoFaceFlanks,
            flankAutoFaceDelay = flankAutoFaceDelay,
            faceDamageMultiplier = faceDamageMultiplier,
            flankDamageMultiplier = flankDamageMultiplier,
            backDamageMultiplier = backDamageMultiplier
        };
    }

    internal CombatSpatialSettings SanitizedCopy()
    {
        CombatSpatialSettings copy = Copy();
        copy.SanitizeInPlace();
        return copy;
    }

    internal void SanitizeInPlace()
    {
        minimumDistance = Mathf.Max(0.01f, minimumDistance);
        maximumDistance = Mathf.Max(
            minimumDistance + 0.02f,
            maximumDistance
        );
        midRangeDistance = Mathf.Clamp(
            midRangeDistance,
            minimumDistance,
            maximumDistance
        );
        closeRangeUpperBound = Mathf.Clamp(
            closeRangeUpperBound,
            minimumDistance,
            maximumDistance - 0.01f
        );
        midRangeUpperBound = Mathf.Clamp(
            midRangeUpperBound,
            closeRangeUpperBound + 0.01f,
            maximumDistance
        );
        advanceSpeed = Mathf.Max(0f, advanceSpeed);
        retreatSpeed = Mathf.Max(0f, retreatSpeed);
        strafeSpeed = Mathf.Max(0f, strafeSpeed);
        rotationSpeed = Mathf.Max(0.01f, rotationSpeed);
        dodgeOrientationAngle = Mathf.Clamp(
            dodgeOrientationAngle,
            0f,
            180f
        );
        flankAutoFaceDelay = Mathf.Max(
            0f,
            flankAutoFaceDelay
        );
        faceDamageMultiplier = Mathf.Max(
            0f,
            faceDamageMultiplier
        );
        flankDamageMultiplier = Mathf.Max(
            0f,
            flankDamageMultiplier
        );
        backDamageMultiplier = Mathf.Max(
            0f,
            backDamageMultiplier
        );
    }

    internal bool HasSameValues(CombatSpatialSettings other)
    {
        return other != null &&
               Mathf.Approximately(
                   minimumDistance,
                   other.minimumDistance
               ) &&
               Mathf.Approximately(
                   closeRangeUpperBound,
                   other.closeRangeUpperBound
               ) &&
               Mathf.Approximately(
                   midRangeDistance,
                   other.midRangeDistance
               ) &&
               Mathf.Approximately(
                   midRangeUpperBound,
                   other.midRangeUpperBound
               ) &&
               Mathf.Approximately(
                   maximumDistance,
                   other.maximumDistance
               ) &&
               Mathf.Approximately(
                   advanceSpeed,
                   other.advanceSpeed
               ) &&
               Mathf.Approximately(
                   retreatSpeed,
                   other.retreatSpeed
               ) &&
               Mathf.Approximately(
                    strafeSpeed,
                    other.strafeSpeed
                ) &&
               Mathf.Approximately(
                    rotationSpeed,
                    other.rotationSpeed
                ) &&
               Mathf.Approximately(
                    dodgeOrientationAngle,
                    other.dodgeOrientationAngle
               ) &&
               autoFaceFlanks == other.autoFaceFlanks &&
               Mathf.Approximately(
                   flankAutoFaceDelay,
                   other.flankAutoFaceDelay
               ) &&
               Mathf.Approximately(
                   faceDamageMultiplier,
                   other.faceDamageMultiplier
               ) &&
               Mathf.Approximately(
                   flankDamageMultiplier,
                   other.flankDamageMultiplier
               ) &&
               Mathf.Approximately(
                   backDamageMultiplier,
                   other.backDamageMultiplier
               );
    }
}

public readonly struct SpatialDodgeTransaction
{
    public long Id { get; }
    public long Epoch { get; }
    public int PreparedRevision { get; }
    public FighterCombat Fighter { get; }
    public FighterCombat OtherFighter { get; }
    public FighterCombat AdvantageAfterCommit { get; }
    public DodgeDirection Direction { get; }
    public RelativeOrientation OrientationBefore { get; }
    public RelativeOrientation OrientationAfter { get; }
    public DistanceLevel DistanceBefore { get; }
    public DistanceLevel DistanceAfter { get; }
    public Pose FirstStartPose { get; }
    public Pose SecondStartPose { get; }
    public Pose FirstEndPose { get; }
    public Pose SecondEndPose { get; }
    public bool IsValid => Id > 0 && Fighter != null;

    internal SpatialDodgeTransaction(
        long id,
        long epoch,
        int preparedRevision,
        FighterCombat fighter,
        FighterCombat otherFighter,
        FighterCombat advantageAfterCommit,
        DodgeDirection direction,
        RelativeOrientation orientationBefore,
        RelativeOrientation orientationAfter,
        DistanceLevel distanceBefore,
        DistanceLevel distanceAfter,
        Pose firstStartPose,
        Pose secondStartPose,
        Pose firstEndPose,
        Pose secondEndPose)
    {
        Id = id;
        Epoch = epoch;
        PreparedRevision = preparedRevision;
        Fighter = fighter;
        OtherFighter = otherFighter;
        AdvantageAfterCommit = advantageAfterCommit;
        Direction = direction;
        OrientationBefore = orientationBefore;
        OrientationAfter = orientationAfter;
        DistanceBefore = distanceBefore;
        DistanceAfter = distanceAfter;
        FirstStartPose = firstStartPose;
        SecondStartPose = secondStartPose;
        FirstEndPose = firstEndPose;
        SecondEndPose = secondEndPose;
    }
}

public readonly struct CombatSpatialSnapshot
{
    public int Revision { get; }
    public long Epoch { get; }
    public long DodgeEpoch => Epoch;
    public bool IsInitialized { get; }
    public bool IsCombatEnabled { get; }
    public FighterCombat FirstFighter { get; }
    public FighterCombat SecondFighter { get; }
    public FighterCombat AdvantageFighter { get; }
    public DistanceLevel Distance { get; }
    public RelativeOrientation Orientation { get; }
    public float Separation { get; }
    public float FlankAutoFaceRemaining { get; }
    public SpatialMovementType FirstMovement { get; }
    public SpatialMovementType SecondMovement { get; }
    public Pose FirstNeutralPose { get; }
    public Pose SecondNeutralPose { get; }
    public bool HasPendingDodge { get; }
    public long PendingDodgeId { get; }
    public float AdvantageDamageMultiplier { get; }
    public bool CanApplyPermutation { get; }
    public bool CanPermutePositions => CanApplyPermutation;

    internal CombatSpatialSnapshot(
        int revision,
        long dodgeEpoch,
        bool isInitialized,
        bool isCombatEnabled,
        FighterCombat firstFighter,
        FighterCombat secondFighter,
        FighterCombat advantageFighter,
        DistanceLevel distance,
        RelativeOrientation orientation,
        float separation,
        float flankAutoFaceRemaining,
        SpatialMovementType firstMovement,
        SpatialMovementType secondMovement,
        Pose firstNeutralPose,
        Pose secondNeutralPose,
        bool hasPendingDodge,
        long pendingDodgeId,
        float advantageDamageMultiplier,
        bool canApplyPermutation)
    {
        Revision = revision;
        Epoch = dodgeEpoch;
        IsInitialized = isInitialized;
        IsCombatEnabled = isCombatEnabled;
        FirstFighter = firstFighter;
        SecondFighter = secondFighter;
        AdvantageFighter = advantageFighter;
        Distance = distance;
        Orientation = orientation;
        Separation = separation;
        FlankAutoFaceRemaining = flankAutoFaceRemaining;
        FirstMovement = firstMovement;
        SecondMovement = secondMovement;
        FirstNeutralPose = firstNeutralPose;
        SecondNeutralPose = secondNeutralPose;
        HasPendingDodge = hasPendingDodge;
        PendingDodgeId = pendingDodgeId;
        AdvantageDamageMultiplier = advantageDamageMultiplier;
        CanApplyPermutation = canApplyPermutation;
    }
}

public readonly struct CombatSpatialTelemetry
{
    public CombatSpatialChangeReason Reason { get; }
    public CombatSpatialSnapshot Snapshot { get; }
    public FighterCombat Instigator { get; }
    public long DodgeTransactionId { get; }

    internal CombatSpatialTelemetry(
        CombatSpatialChangeReason reason,
        CombatSpatialSnapshot snapshot,
        FighterCombat instigator,
        long dodgeTransactionId)
    {
        Reason = reason;
        Snapshot = snapshot;
        Instigator = instigator;
        DodgeTransactionId = dodgeTransactionId;
    }
}

[DisallowMultipleComponent]
public sealed class CombatSpatialController : MonoBehaviour
{
    private const float PositionEpsilon = 0.000001f;
    private const float RotationEpsilon = 0.01f;

    [Header("Duel")]
    [SerializeField] private FighterCombat firstFighter;
    [SerializeField] private FighterCombat secondFighter;

    [Header("Reglages spatiaux")]
    [SerializeField]
    private CombatSpatialSettings settings = new();

    private bool initialized;
    private bool combatEnabled = true;
    private Pose firstResetPose;
    private Pose secondResetPose;
    private Pose firstNeutralPose;
    private Pose secondNeutralPose;
    private DistanceLevel distanceLevel = DistanceLevel.MidRange;
    private RelativeOrientation relativeOrientation =
        RelativeOrientation.Face;
    private SpatialMovementType firstMovement;
    private SpatialMovementType secondMovement;
    private FighterCombat advantageFighter;
    private DodgeDirection? flankDodgeDirection;
    private float flankElapsed;
    private bool hasPendingDodge;
    private SpatialDodgeTransaction pendingDodge;
    private long nextDodgeId = 1;
    private long dodgeEpoch;
    private int revision;

    public event Action<CombatSpatialSnapshot> OnSnapshotChanged;
    public event Action<CombatSpatialTelemetry> OnTelemetry;
    public event Action<
        RelativeOrientation,
        RelativeOrientation> OnOrientationChanged;
    public event Action<SpatialDodgeTransaction> OnDodgePrepared;
    public event Action<SpatialDodgeTransaction> OnDodgeCommitted;
    public event Action<SpatialDodgeTransaction> OnDodgeCancelled;

    public bool IsInitialized =>
        initialized &&
        firstFighter != null &&
        secondFighter != null;
    public bool IsCombatEnabled => combatEnabled;
    public long Epoch => dodgeEpoch;
    public int Revision => revision;
    public FighterCombat FirstFighter => firstFighter;
    public FighterCombat SecondFighter => secondFighter;
    public FighterCombat AdvantageFighter => advantageFighter;
    public DistanceLevel CurrentDistance => distanceLevel;
    public RelativeOrientation CurrentOrientation =>
        relativeOrientation;
    public Pose FirstNeutralPose => firstNeutralPose;
    public Pose SecondNeutralPose => secondNeutralPose;
    public Vector3 FirstNeutralPosition =>
        firstNeutralPose.position;
    public Vector3 SecondNeutralPosition =>
        secondNeutralPose.position;
    public bool HasPendingDodge => hasPendingDodge;
    public SpatialDodgeTransaction PendingDodge =>
        pendingDodge;
    public CombatSpatialSettings Configuration =>
        settings.Copy();
    public CombatSpatialSnapshot Snapshot =>
        CreateSnapshot();
    public bool CanApplyPermutation =>
        IsInitialized &&
        combatEnabled;
    public bool CanPermutePositions => CanApplyPermutation;

    public bool IsCurrentDistanceAllowed(
        DistanceLevel minimum,
        DistanceLevel maximum)
    {
        if (!IsInitialized)
            return true;

        int lower = Mathf.Min((int)minimum, (int)maximum);
        int upper = Mathf.Max((int)minimum, (int)maximum);
        int current = (int)distanceLevel;
        return current >= lower && current <= upper;
    }

    public float GetDistance(DistanceLevel level)
    {
        return settings.GetDistance(level);
    }

    public bool CanDodge(
        FighterCombat fighter,
        DodgeDirection direction)
    {
        if (!IsInitialized ||
            !combatEnabled ||
            hasPendingDodge ||
            !Contains(fighter) ||
            !IsKnownDodgeDirection(direction))
        {
            return false;
        }

        if (direction == DodgeDirection.Forward)
            return distanceLevel != DistanceLevel.CloseRange;
        if (direction == DodgeDirection.Backward)
            return distanceLevel != DistanceLevel.LongRange;

        return TryResolveDodgeTransition(
            fighter,
            direction,
            out _,
            out _
        );
    }

    private void Awake()
    {
        settings ??= new CombatSpatialSettings();
        settings.SanitizeInPlace();

        if (firstFighter != null && secondFighter != null)
            Initialize(firstFighter, secondFighter);
    }

    private void OnValidate()
    {
        settings ??= new CombatSpatialSettings();
        settings.SanitizeInPlace();
    }

    private void Update()
    {
        if (!IsInitialized ||
            !combatEnabled ||
            hasPendingDodge)
            return;

        float deltaTime = Time.deltaTime;
        UpdateContinuousMovement(deltaTime);
        UpdateAutoFace(deltaTime);
    }

    public bool Initialize(
        FighterCombat first,
        FighterCombat second)
    {
        if (first == null || second == null || first == second)
        {
            Debug.LogError(
                "CombatSpatialController requires two distinct fighters.",
                this
            );
            return false;
        }

        if (IsInitialized &&
            firstFighter == first &&
            secondFighter == second)
        {
            return true;
        }

        firstFighter = first;
        secondFighter = second;
        firstResetPose = ReadPose(first.transform);
        secondResetPose = ReadPose(second.transform);
        NormalizeResetPosesToMidRange();
        firstNeutralPose = firstResetPose;
        secondNeutralPose = secondResetPose;
        distanceLevel = DistanceLevel.MidRange;
        relativeOrientation = RelativeOrientation.Face;
        firstMovement = SpatialMovementType.None;
        secondMovement = SpatialMovementType.None;
        advantageFighter = null;
        flankDodgeDirection = null;
        flankElapsed = 0f;
        hasPendingDodge = false;
        pendingDodge = default;
        combatEnabled = true;
        AdvanceDodgeEpoch();
        initialized = true;
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.Initialized,
            null,
            0
        );
        return true;
    }

    public void Configure(
        CombatSpatialSettings configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        CombatSpatialSettings sanitized =
            configuration.SanitizedCopy();
        if (settings != null &&
            settings.HasSameValues(sanitized))
        {
            return;
        }

        settings = sanitized;
        if (IsInitialized)
        {
            NormalizeResetPosesToMidRange();
            ConstrainNeutralDistance();
            ApplyNeutralPosesToTransforms();
        }

        Publish(
            CombatSpatialChangeReason.ConfigurationChanged,
            null,
            0
        );
    }

    public bool StartMovement(
        FighterCombat fighter,
        SpatialMovementType movement)
    {
        if (!IsInitialized ||
            !combatEnabled ||
            hasPendingDodge ||
            !IsKnownMovement(movement) ||
            !Contains(fighter))
        {
            return false;
        }

        if (movement == SpatialMovementType.None)
        {
            StopMovement(fighter);
            return true;
        }

        if (!IsStrafe(movement))
            return false;

        if (IsStrafe(movement) &&
            relativeOrientation != RelativeOrientation.Face)
        {
            return false;
        }

        SpatialMovementType current =
            fighter == firstFighter
                ? firstMovement
                : secondMovement;
        if (current == movement)
            return true;

        if (fighter == firstFighter)
            firstMovement = movement;
        else
            secondMovement = movement;

        Publish(
            CombatSpatialChangeReason.MovementInputChanged,
            fighter,
            0
        );
        return true;
    }

    public void StopMovement(FighterCombat fighter)
    {
        if (!IsInitialized || !Contains(fighter))
            return;

        bool changed;
        if (fighter == firstFighter)
        {
            changed =
                firstMovement != SpatialMovementType.None;
            firstMovement = SpatialMovementType.None;
        }
        else
        {
            changed =
                secondMovement != SpatialMovementType.None;
            secondMovement = SpatialMovementType.None;
        }

        if (changed)
        {
            Publish(
                CombatSpatialChangeReason.MovementInputChanged,
                fighter,
                0
            );
        }
    }

    public void StopAllMovement()
    {
        if (!IsInitialized || !StopAllMovementInternal())
            return;

        Publish(
            CombatSpatialChangeReason.MovementInputChanged,
            null,
            0
        );
    }

    public bool TryPrepareDodge(
        FighterCombat fighter,
        DodgeDirection direction,
        out SpatialDodgeTransaction transaction)
    {
        transaction = default;
        if (!IsInitialized ||
            !combatEnabled ||
            hasPendingDodge ||
            !Contains(fighter) ||
            !IsKnownDodgeDirection(direction))
        {
            return false;
        }

        RelativeOrientation orientationAfter =
            relativeOrientation;
        FighterCombat advantageAfter = advantageFighter;
        DistanceLevel distanceAfter = distanceLevel;

        if (direction is DodgeDirection.Forward or
            DodgeDirection.Backward)
        {
            distanceAfter = ResolveDodgeDistance(
                distanceLevel,
                direction
            );
            if (distanceAfter == distanceLevel)
                return false;
        }
        else if (!TryResolveDodgeTransition(
                     fighter,
                     direction,
                     out orientationAfter,
                     out advantageAfter))
        {
            return false;
        }

        FighterCombat other = GetOtherFighter(fighter);
        CalculateDodgeEndPoses(
            fighter,
            direction,
            orientationAfter,
            out Pose firstEndPose,
            out Pose secondEndPose
        );

        StopAllMovementInternal();
        long transactionId = AllocateDodgeId();
        transaction = new SpatialDodgeTransaction(
            transactionId,
            dodgeEpoch,
            revision + 1,
            fighter,
            other,
            advantageAfter,
            direction,
            relativeOrientation,
            orientationAfter,
            distanceLevel,
            distanceAfter,
            firstNeutralPose,
            secondNeutralPose,
            firstEndPose,
            secondEndPose
        );
        pendingDodge = transaction;
        hasPendingDodge = true;

        Publish(
            CombatSpatialChangeReason.DodgePrepared,
            fighter,
            transactionId
        );
        OnDodgePrepared?.Invoke(transaction);
        return true;
    }

    public bool PreviewPreparedDodge(
        long transactionId,
        float normalizedProgress)
    {
        if (!IsPendingTransaction(transactionId))
            return false;

        float progress = Mathf.Clamp01(normalizedProgress);
        ApplyPose(
            firstFighter.transform,
            LerpPose(
                pendingDodge.FirstStartPose,
                pendingDodge.FirstEndPose,
                progress
            )
        );
        ApplyPose(
            secondFighter.transform,
            LerpPose(
                pendingDodge.SecondStartPose,
                pendingDodge.SecondEndPose,
                progress
            )
        );
        return true;
    }

    public bool CommitDodge(long transactionId)
    {
        if (!IsPendingTransaction(transactionId))
            return false;

        SpatialDodgeTransaction committed = pendingDodge;
        RelativeOrientation previousOrientation =
            relativeOrientation;

        firstNeutralPose = committed.FirstEndPose;
        secondNeutralPose = committed.SecondEndPose;
        relativeOrientation =
            committed.OrientationAfter;
        distanceLevel = committed.DistanceAfter;
        advantageFighter =
            committed.AdvantageAfterCommit;
        if (relativeOrientation == RelativeOrientation.Face)
        {
            flankDodgeDirection = null;
        }
        else if (committed.Direction is
                 DodgeDirection.Left or
                 DodgeDirection.Right)
        {
            flankDodgeDirection = committed.Direction;
        }
        flankElapsed = 0f;
        hasPendingDodge = false;
        pendingDodge = default;

        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.DodgeCommitted,
            committed.Fighter,
            committed.Id
        );
        if (previousOrientation != relativeOrientation)
        {
            OnOrientationChanged?.Invoke(
                previousOrientation,
                relativeOrientation
            );
        }
        OnDodgeCommitted?.Invoke(committed);
        return true;
    }

    public bool CommitDodge(
        SpatialDodgeTransaction transaction)
    {
        return transaction.Epoch == dodgeEpoch &&
               CommitDodge(transaction.Id);
    }

    public bool CancelDodge(long transactionId)
    {
        if (!IsPendingTransaction(transactionId))
            return false;

        SpatialDodgeTransaction cancelled = pendingDodge;
        hasPendingDodge = false;
        pendingDodge = default;
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.DodgeCancelled,
            cancelled.Fighter,
            cancelled.Id
        );
        OnDodgeCancelled?.Invoke(cancelled);
        return true;
    }

    public bool CancelDodge(
        SpatialDodgeTransaction transaction)
    {
        return transaction.Epoch == dodgeEpoch &&
               CancelDodge(transaction.Id);
    }

    internal bool ApplyPermutation(FighterCombat instigator)
    {
        if (!CanApplyPermutation || !Contains(instigator))
            return false;

        RelativeOrientation previousOrientation =
            relativeOrientation;
        SpatialDodgeTransaction cancelled =
            hasPendingDodge ? pendingDodge : default;
        StopAllMovementInternal();
        hasPendingDodge = false;
        pendingDodge = default;
        DistanceLevel nextDistance = distanceLevel switch
        {
            DistanceLevel.CloseRange => DistanceLevel.MidRange,
            DistanceLevel.MidRange => DistanceLevel.LongRange,
            _ => DistanceLevel.CloseRange
        };
        MoveFighterToDistanceAnchor(
            instigator,
            nextDistance
        );
        relativeOrientation = RelativeOrientation.Face;
        distanceLevel = nextDistance;
        advantageFighter = null;
        flankDodgeDirection = null;
        flankElapsed = 0f;
        SetFaceRotations();
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.PositionsPermuted,
            instigator,
            cancelled.Id
        );
        if (previousOrientation != relativeOrientation)
        {
            OnOrientationChanged?.Invoke(
                previousOrientation,
                relativeOrientation
            );
        }
        if (cancelled.IsValid)
            OnDodgeCancelled?.Invoke(cancelled);
        return true;
    }

    public float GetDamageMultiplier(
        FighterCombat attacker,
        FighterCombat defender)
    {
        return GetOrientationDamageMultiplier(
            GetAttackOrientation(attacker, defender)
        );
    }

    public bool CanAttackTarget(
        FighterCombat attacker,
        FighterCombat defender)
    {
        if (!IsInitialized ||
            !Contains(attacker) ||
            defender != GetOtherFighter(attacker))
        {
            return true;
        }

        return relativeOrientation == RelativeOrientation.Face ||
               advantageFighter == attacker;
    }

    public RelativeOrientation GetAttackOrientation(
        FighterCombat attacker,
        FighterCombat defender)
    {
        bool hasSpatialAdvantage =
            IsInitialized &&
            relativeOrientation != RelativeOrientation.Face &&
            attacker == advantageFighter &&
            defender == GetOtherFighter(attacker);
        return hasSpatialAdvantage
            ? relativeOrientation
            : RelativeOrientation.Face;
    }

    public float ResolveDamage(
        float baseDamage,
        FighterCombat attacker,
        FighterCombat defender)
    {
        return Mathf.Max(0f, baseDamage) *
               GetDamageMultiplier(attacker, defender);
    }

    public bool TryGetNeutralPose(
        FighterCombat fighter,
        out Pose neutralPose)
    {
        if (fighter == firstFighter && IsInitialized)
        {
            neutralPose = firstNeutralPose;
            return true;
        }

        if (fighter == secondFighter && IsInitialized)
        {
            neutralPose = secondNeutralPose;
            return true;
        }

        neutralPose = default;
        return false;
    }

    public bool TryGetNeutralPosition(
        FighterCombat fighter,
        out Vector3 neutralPosition)
    {
        bool found = TryGetNeutralPose(
            fighter,
            out Pose neutralPose
        );
        neutralPosition = neutralPose.position;
        return found;
    }

    public bool RestoreNeutralPose(FighterCombat fighter)
    {
        if (!TryGetNeutralPose(fighter, out Pose neutralPose))
            return false;

        Transform target = fighter.transform;
        if (TransformMatchesPose(target, neutralPose))
            return true;

        ApplyPose(target, neutralPose);
        return true;
    }

    public void RestoreNeutralPoses()
    {
        if (IsInitialized)
            ApplyNeutralPosesToTransforms();
    }

    public void NotifySignificantAction()
    {
        if (!IsInitialized ||
            !IsFlank(relativeOrientation) ||
            Mathf.Approximately(flankElapsed, 0f))
        {
            return;
        }

        flankElapsed = 0f;
        Publish(
            CombatSpatialChangeReason.SignificantAction,
            null,
            0
        );
    }

    public void SetCombatEnabled(bool enabled)
    {
        if (!IsInitialized)
        {
            combatEnabled = enabled;
            return;
        }

        if (combatEnabled == enabled)
            return;

        combatEnabled = enabled;
        SpatialDodgeTransaction cancelled = default;
        if (!enabled)
        {
            StopAllMovementInternal();
            if (hasPendingDodge)
            {
                cancelled = pendingDodge;
                hasPendingDodge = false;
                pendingDodge = default;
                AdvanceDodgeEpoch();
            }

            flankElapsed = 0f;
            ApplyNeutralPosesToTransforms();
        }

        Publish(
            CombatSpatialChangeReason.CombatEnabledChanged,
            null,
            cancelled.Id
        );
        if (cancelled.IsValid)
            OnDodgeCancelled?.Invoke(cancelled);
    }

    public void CancelTransientState()
    {
        if (!IsInitialized)
            return;

        bool changed = StopAllMovementInternal();
        SpatialDodgeTransaction cancelled = default;
        if (hasPendingDodge)
        {
            cancelled = pendingDodge;
            hasPendingDodge = false;
            pendingDodge = default;
            changed = true;
        }

        if (!NeutralTransformsMatch())
        {
            ApplyNeutralPosesToTransforms();
            changed = true;
        }

        if (!changed)
            return;

        Publish(
            CombatSpatialChangeReason.TransientStateCancelled,
            cancelled.Fighter,
            cancelled.Id
        );
        if (cancelled.IsValid)
            OnDodgeCancelled?.Invoke(cancelled);
    }

    public void ResetDuel()
    {
        if (!IsInitialized)
            return;

        SpatialDodgeTransaction cancelled =
            hasPendingDodge ? pendingDodge : default;
        RelativeOrientation previousOrientation =
            relativeOrientation;
        bool changed =
            firstMovement != SpatialMovementType.None ||
            secondMovement != SpatialMovementType.None ||
            hasPendingDodge ||
            !combatEnabled ||
            distanceLevel != DistanceLevel.MidRange ||
            relativeOrientation != RelativeOrientation.Face ||
            advantageFighter != null ||
            flankDodgeDirection.HasValue ||
            flankElapsed > 0f ||
            !PosesMatch(firstNeutralPose, firstResetPose) ||
            !PosesMatch(secondNeutralPose, secondResetPose) ||
            !TransformMatchesPose(
                firstFighter.transform,
                firstResetPose
            ) ||
            !TransformMatchesPose(
                secondFighter.transform,
                secondResetPose
            );

        if (!changed)
            return;

        firstMovement = SpatialMovementType.None;
        secondMovement = SpatialMovementType.None;
        hasPendingDodge = false;
        pendingDodge = default;
        combatEnabled = true;
        AdvanceDodgeEpoch();
        firstNeutralPose = firstResetPose;
        secondNeutralPose = secondResetPose;
        distanceLevel = DistanceLevel.MidRange;
        relativeOrientation = RelativeOrientation.Face;
        advantageFighter = null;
        flankDodgeDirection = null;
        flankElapsed = 0f;
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.DuelReset,
            null,
            cancelled.Id
        );
        if (previousOrientation != relativeOrientation)
        {
            OnOrientationChanged?.Invoke(
                previousOrientation,
                relativeOrientation
            );
        }
        if (cancelled.IsValid)
            OnDodgeCancelled?.Invoke(cancelled);
    }

    private void UpdateContinuousMovement(float deltaTime)
    {
        if (deltaTime <= 0f ||
            (firstMovement == SpatialMovementType.None &&
             secondMovement == SpatialMovementType.None))
        {
            return;
        }

        Vector3 firstPosition = firstNeutralPose.position;
        Vector3 secondPosition = secondNeutralPose.position;
        bool moved = false;

        float firstStrafeInput =
            GetStrafeInput(firstMovement);
        float secondStrafeInput =
            GetStrafeInput(secondMovement);
        if (relativeOrientation == RelativeOrientation.Face &&
            (!Mathf.Approximately(firstStrafeInput, 0f) ||
             !Mathf.Approximately(secondStrafeInput, 0f)))
        {
            if (!Mathf.Approximately(firstStrafeInput, 0f) &&
                Mathf.Approximately(secondStrafeInput, 0f))
            {
                RotateFighterAroundOpponent(
                    ref firstPosition,
                    secondPosition,
                    firstStrafeInput,
                    deltaTime
                );
                moved = true;
            }
            else if (
                Mathf.Approximately(firstStrafeInput, 0f) &&
                !Mathf.Approximately(secondStrafeInput, 0f))
            {
                RotateFighterAroundOpponent(
                    ref secondPosition,
                    firstPosition,
                    secondStrafeInput,
                    deltaTime
                );
                moved = true;
            }
            else
            {
                float combinedInput = Mathf.Clamp(
                    firstStrafeInput + secondStrafeInput,
                    -1f,
                    1f
                );
                if (!Mathf.Approximately(combinedInput, 0f))
                {
                    float orbitRadius = Mathf.Max(
                        0.01f,
                        Horizontal(
                            secondPosition - firstPosition
                        ).magnitude * 0.5f
                    );
                    float degreesPerSecond =
                        settings.StrafeSpeed /
                        orbitRadius *
                        Mathf.Rad2Deg;
                    RotatePairAroundMidpoint(
                        ref firstPosition,
                        ref secondPosition,
                        combinedInput *
                        degreesPerSecond *
                        deltaTime
                    );
                    moved = true;
                }
            }
        }

        if (!moved)
            return;

        firstNeutralPose = new Pose(
            firstPosition,
            firstNeutralPose.rotation
        );
        secondNeutralPose = new Pose(
            secondPosition,
            secondNeutralPose.rotation
        );
        RefreshNormalRotations();
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.ContinuousMovement,
            null,
            0
        );
    }

    private void UpdateAutoFace(float deltaTime)
    {
        if (!settings.AutoFaceFlanks ||
            !IsFlank(relativeOrientation) ||
            hasPendingDodge ||
            firstMovement != SpatialMovementType.None ||
            secondMovement != SpatialMovementType.None ||
            firstFighter.CurrentState != FighterCombatState.Idle ||
            secondFighter.CurrentState != FighterCombatState.Idle)
        {
            return;
        }

        flankElapsed += Mathf.Max(0f, deltaTime);
        if (flankElapsed < settings.FlankAutoFaceDelay)
            return;

        RelativeOrientation previousOrientation =
            relativeOrientation;
        relativeOrientation = RelativeOrientation.Face;
        advantageFighter = null;
        flankDodgeDirection = null;
        flankElapsed = 0f;
        SetFaceRotations();
        ApplyNeutralPosesToTransforms();

        Publish(
            CombatSpatialChangeReason.AutoFaced,
            null,
            0
        );
        OnOrientationChanged?.Invoke(
            previousOrientation,
            relativeOrientation
        );
    }

    private bool TryResolveDodgeTransition(
        FighterCombat fighter,
        DodgeDirection direction,
        out RelativeOrientation orientationAfter,
        out FighterCombat advantageAfter)
    {
        orientationAfter = relativeOrientation;
        advantageAfter = advantageFighter;

        if (relativeOrientation == RelativeOrientation.Face)
        {
            orientationAfter =
                direction == DodgeDirection.Left
                    ? RelativeOrientation.LeftFlank
                    : RelativeOrientation.RightFlank;
            advantageAfter = fighter;
            return true;
        }

        if (relativeOrientation == RelativeOrientation.Back)
        {
            orientationAfter =
                direction == DodgeDirection.Left
                    ? RelativeOrientation.LeftFlank
                    : RelativeOrientation.RightFlank;
            advantageAfter = fighter;
            return true;
        }

        if (!IsFlank(relativeOrientation) ||
            !flankDodgeDirection.HasValue)
        {
            return false;
        }

        bool sameDirection =
            direction == flankDodgeDirection.Value;
        if (advantageFighter != fighter)
        {
            if (sameDirection)
            {
                orientationAfter = RelativeOrientation.Face;
                advantageAfter = null;
            }
            else
            {
                orientationAfter = RelativeOrientation.Back;
                advantageAfter = fighter;
            }
            return true;
        }

        if (sameDirection)
        {
            orientationAfter = RelativeOrientation.Back;
            advantageAfter = fighter;
        }
        else
        {
            orientationAfter = RelativeOrientation.Face;
            advantageAfter = null;
        }

        return true;
    }

    private void CalculateDodgeEndPoses(
        FighterCombat fighter,
        DodgeDirection direction,
        RelativeOrientation orientationAfter,
        out Pose firstEndPose,
        out Pose secondEndPose)
    {
        if (direction is DodgeDirection.Forward or
            DodgeDirection.Backward)
        {
            CalculateDistanceDodgeEndPoses(
                fighter,
                ResolveDodgeDistance(
                    distanceLevel,
                    direction
                ),
                out firstEndPose,
                out secondEndPose
            );
            return;
        }

        bool firstIsDodging = fighter == firstFighter;
        Pose fighterPose =
            firstIsDodging
                ? firstNeutralPose
                : secondNeutralPose;
        Pose otherPose =
            firstIsDodging
                ? secondNeutralPose
                : firstNeutralPose;
        Vector3 fighterPosition = fighterPose.position;
        Vector3 otherPosition = otherPose.position;
        Vector3 relative =
            Horizontal(fighterPosition - otherPosition);
        if (relative.sqrMagnitude <= PositionEpsilon)
        {
            relative =
                -Horizontal(fighter.transform.forward);
            if (relative.sqrMagnitude <= PositionEpsilon)
                relative = Vector3.back;
            relative =
                relative.normalized *
                settings.MinimumDistance;
        }

        float angle =
            direction == DodgeDirection.Left
                ? settings.DodgeOrientationAngle
                : -settings.DodgeOrientationAngle;
        Vector3 rotatedRelative =
            Quaternion.AngleAxis(angle, Vector3.up) *
            relative;
        Vector3 midpoint =
            (fighterPosition + otherPosition) * 0.5f;
        Vector3 fighterEndPosition =
            midpoint + rotatedRelative * 0.5f;
        Vector3 otherEndPosition =
            midpoint - rotatedRelative * 0.5f;
        fighterEndPosition.y = fighterPosition.y;
        otherEndPosition.y = otherPosition.y;

        Pose fighterEndPose = new(
            fighterEndPosition,
            FacingRotation(
                fighterEndPosition,
                otherEndPosition,
                fighterPose.rotation
            )
        );
        Pose otherEndPose = new(
            otherEndPosition,
            otherPose.rotation
        );

        if (orientationAfter == RelativeOrientation.Face)
        {
            otherEndPose = new Pose(
                otherEndPosition,
                FacingRotation(
                    otherEndPosition,
                    fighterEndPosition,
                    otherPose.rotation
                )
            );
        }

        if (firstIsDodging)
        {
            firstEndPose = fighterEndPose;
            secondEndPose = otherEndPose;
        }
        else
        {
            firstEndPose = otherEndPose;
            secondEndPose = fighterEndPose;
        }
    }

    private void CalculateDistanceDodgeEndPoses(
        FighterCombat fighter,
        DistanceLevel targetDistance,
        out Pose firstEndPose,
        out Pose secondEndPose)
    {
        bool firstIsDodging = fighter == firstFighter;
        Pose fighterPose =
            firstIsDodging ? firstNeutralPose : secondNeutralPose;
        Pose otherPose =
            firstIsDodging ? secondNeutralPose : firstNeutralPose;
        Vector3 radial =
            Horizontal(fighterPose.position - otherPose.position);
        if (radial.sqrMagnitude <= PositionEpsilon)
            radial = -Horizontal(otherPose.rotation * Vector3.forward);
        if (radial.sqrMagnitude <= PositionEpsilon)
            radial = Vector3.back;

        Vector3 fighterPosition =
            otherPose.position +
            radial.normalized * settings.GetDistance(targetDistance);
        fighterPosition.y = fighterPose.position.y;
        Pose fighterEndPose = new(
            fighterPosition,
            fighterPose.rotation
        );
        Pose otherEndPose = otherPose;

        if (relativeOrientation == RelativeOrientation.Face)
        {
            fighterEndPose = new Pose(
                fighterPosition,
                FacingRotation(
                    fighterPosition,
                    otherPose.position,
                    fighterPose.rotation
                )
            );
            otherEndPose = new Pose(
                otherPose.position,
                FacingRotation(
                    otherPose.position,
                    fighterPosition,
                    otherPose.rotation
                )
            );
        }

        if (firstIsDodging)
        {
            firstEndPose = fighterEndPose;
            secondEndPose = otherEndPose;
        }
        else
        {
            firstEndPose = otherEndPose;
            secondEndPose = fighterEndPose;
        }
    }

    private void MoveFighterToDistanceAnchor(
        FighterCombat fighter,
        DistanceLevel targetDistance)
    {
        CalculateDistanceDodgeEndPoses(
            fighter,
            targetDistance,
            out Pose firstEndPose,
            out Pose secondEndPose
        );
        firstNeutralPose = firstEndPose;
        secondNeutralPose = secondEndPose;
    }

    private static DistanceLevel ResolveDodgeDistance(
        DistanceLevel current,
        DodgeDirection direction)
    {
        if (direction == DodgeDirection.Forward)
        {
            return current switch
            {
                DistanceLevel.LongRange => DistanceLevel.MidRange,
                DistanceLevel.MidRange => DistanceLevel.CloseRange,
                _ => DistanceLevel.CloseRange
            };
        }

        if (direction == DodgeDirection.Backward)
        {
            return current switch
            {
                DistanceLevel.CloseRange => DistanceLevel.MidRange,
                DistanceLevel.MidRange => DistanceLevel.LongRange,
                _ => DistanceLevel.LongRange
            };
        }

        return current;
    }

    private void ConstrainNeutralDistance()
    {
        Vector3 firstPosition = firstNeutralPose.position;
        Vector3 secondPosition = secondNeutralPose.position;
        Vector3 separation =
            Horizontal(secondPosition - firstPosition);
        Vector3 direction =
            separation.sqrMagnitude > PositionEpsilon
                ? separation.normalized
                : GetFallbackDuelDirection();
        if (direction.sqrMagnitude <= PositionEpsilon)
            direction = Vector3.forward;
        Vector3 midpoint =
            (firstPosition + secondPosition) * 0.5f;
        float halfDistance =
            settings.GetDistance(distanceLevel) * 0.5f;
        float firstY = firstPosition.y;
        float secondY = secondPosition.y;
        firstPosition = midpoint - direction * halfDistance;
        secondPosition = midpoint + direction * halfDistance;
        firstPosition.y = firstY;
        secondPosition.y = secondY;
        firstNeutralPose = new Pose(
            firstPosition,
            firstNeutralPose.rotation
        );
        secondNeutralPose = new Pose(
            secondPosition,
            secondNeutralPose.rotation
        );
        RefreshNormalRotations();
    }

    private void NormalizeResetPosesToMidRange()
    {
        Vector3 firstPosition = firstResetPose.position;
        Vector3 secondPosition = secondResetPose.position;
        Vector3 separation =
            Horizontal(secondPosition - firstPosition);
        Vector3 direction =
            separation.sqrMagnitude > PositionEpsilon
                ? separation.normalized
                : GetFallbackDuelDirection();
        if (direction.sqrMagnitude <= PositionEpsilon)
            direction = Vector3.forward;

        Vector3 midpoint =
            (firstPosition + secondPosition) * 0.5f;
        float halfDistance =
            settings.MidRangeDistance * 0.5f;
        Vector3 normalizedFirst =
            midpoint - direction * halfDistance;
        Vector3 normalizedSecond =
            midpoint + direction * halfDistance;
        normalizedFirst.y = firstPosition.y;
        normalizedSecond.y = secondPosition.y;

        firstResetPose = new Pose(
            normalizedFirst,
            FacingRotation(
                normalizedFirst,
                normalizedSecond,
                firstResetPose.rotation
            )
        );
        secondResetPose = new Pose(
            normalizedSecond,
            FacingRotation(
                normalizedSecond,
                normalizedFirst,
                secondResetPose.rotation
            )
        );
    }

    private void ConstrainPairPositions(
        ref Vector3 firstPosition,
        ref Vector3 secondPosition,
        Vector3 fallbackDirection)
    {
        Vector3 separation =
            Horizontal(secondPosition - firstPosition);
        float distance = separation.magnitude;
        Vector3 direction =
            distance > Mathf.Epsilon
                ? separation / distance
                : fallbackDirection;
        if (direction.sqrMagnitude <= PositionEpsilon)
            direction = Vector3.forward;
        direction.Normalize();

        float constrainedDistance = Mathf.Clamp(
            distance,
            settings.MinimumDistance,
            settings.MaximumDistance
        );
        Vector3 midpoint =
            (firstPosition + secondPosition) * 0.5f;
        float firstY = firstPosition.y;
        float secondY = secondPosition.y;
        firstPosition =
            midpoint - direction * constrainedDistance * 0.5f;
        secondPosition =
            midpoint + direction * constrainedDistance * 0.5f;
        firstPosition.y = firstY;
        secondPosition.y = secondY;
    }

    private static void RotatePairAroundMidpoint(
        ref Vector3 firstPosition,
        ref Vector3 secondPosition,
        float angle)
    {
        Vector3 midpoint =
            (firstPosition + secondPosition) * 0.5f;
        Quaternion rotation =
            Quaternion.AngleAxis(angle, Vector3.up);
        float firstY = firstPosition.y;
        float secondY = secondPosition.y;
        firstPosition =
            midpoint + rotation * (firstPosition - midpoint);
        secondPosition =
            midpoint + rotation * (secondPosition - midpoint);
        firstPosition.y = firstY;
        secondPosition.y = secondY;
    }

    private Vector3 GetRadialVelocity(
        SpatialMovementType movement,
        Vector3 firstToSecond,
        bool isFirstFighter)
    {
        float side = isFirstFighter ? 1f : -1f;
        return movement switch
        {
            SpatialMovementType.Advance =>
                firstToSecond *
                side *
                settings.AdvanceSpeed,
            SpatialMovementType.Retreat =>
                firstToSecond *
                -side *
                settings.RetreatSpeed,
            _ => Vector3.zero
        };
    }

    private void RotateFighterAroundOpponent(
        ref Vector3 fighterPosition,
        Vector3 opponentPosition,
        float strafeInput,
        float deltaTime)
    {
        Vector3 relative =
            Horizontal(fighterPosition - opponentPosition);
        float radius = Mathf.Max(0.01f, relative.magnitude);
        float angle =
            strafeInput *
            settings.StrafeSpeed /
            radius *
            Mathf.Rad2Deg *
            deltaTime;
        float fighterY = fighterPosition.y;
        fighterPosition =
            opponentPosition +
            Quaternion.AngleAxis(angle, Vector3.up) *
            relative;
        fighterPosition.y = fighterY;
    }

    private static float GetStrafeInput(
        SpatialMovementType movement)
    {
        return movement switch
        {
            SpatialMovementType.StrafeLeft => 1f,
            SpatialMovementType.StrafeRight => -1f,
            _ => 0f
        };
    }

    private void RefreshNormalRotations()
    {
        if (relativeOrientation == RelativeOrientation.Face)
        {
            SetFaceRotations();
            return;
        }

        if (advantageFighter == firstFighter)
        {
            firstNeutralPose = new Pose(
                firstNeutralPose.position,
                FacingRotation(
                    firstNeutralPose.position,
                    secondNeutralPose.position,
                    firstNeutralPose.rotation
                )
            );
        }
        else if (advantageFighter == secondFighter)
        {
            secondNeutralPose = new Pose(
                secondNeutralPose.position,
                FacingRotation(
                    secondNeutralPose.position,
                    firstNeutralPose.position,
                    secondNeutralPose.rotation
                )
            );
        }
    }

    private void SetFaceRotations()
    {
        firstNeutralPose = new Pose(
            firstNeutralPose.position,
            FacingRotation(
                firstNeutralPose.position,
                secondNeutralPose.position,
                firstNeutralPose.rotation
            )
        );
        secondNeutralPose = new Pose(
            secondNeutralPose.position,
            FacingRotation(
                secondNeutralPose.position,
                firstNeutralPose.position,
                secondNeutralPose.rotation
            )
        );
    }

    private float GetOrientationDamageMultiplier(
        RelativeOrientation orientation)
    {
        return orientation switch
        {
            RelativeOrientation.LeftFlank =>
                settings.FlankDamageMultiplier,
            RelativeOrientation.RightFlank =>
                settings.FlankDamageMultiplier,
            RelativeOrientation.Back =>
                settings.BackDamageMultiplier,
            _ => settings.FaceDamageMultiplier
        };
    }

    private DistanceLevel ResolveDistanceLevel(float distance)
    {
        if (distance <= settings.CloseRangeUpperBound)
            return DistanceLevel.CloseRange;
        if (distance <= settings.MidRangeUpperBound)
            return DistanceLevel.MidRange;
        return DistanceLevel.LongRange;
    }

    private float GetHorizontalSeparation()
    {
        return Horizontal(
            secondNeutralPose.position -
            firstNeutralPose.position
        ).magnitude;
    }

    private float GetFlankAutoFaceRemaining()
    {
        if (!settings.AutoFaceFlanks ||
            !IsFlank(relativeOrientation))
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            settings.FlankAutoFaceDelay - flankElapsed
        );
    }

    private Vector3 GetFallbackDuelDirection()
    {
        Vector3 direction =
            Horizontal(firstFighter.transform.forward);
        if (direction.sqrMagnitude <= PositionEpsilon)
            direction = Vector3.forward;
        return direction.normalized;
    }

    private FighterCombat GetOtherFighter(
        FighterCombat fighter)
    {
        if (fighter == firstFighter)
            return secondFighter;
        if (fighter == secondFighter)
            return firstFighter;
        return null;
    }

    private bool Contains(FighterCombat fighter)
    {
        return fighter != null &&
               (fighter == firstFighter ||
                fighter == secondFighter);
    }

    private bool StopAllMovementInternal()
    {
        bool changed =
            firstMovement != SpatialMovementType.None ||
            secondMovement != SpatialMovementType.None;
        firstMovement = SpatialMovementType.None;
        secondMovement = SpatialMovementType.None;
        return changed;
    }

    private bool IsPendingTransaction(long transactionId)
    {
        return IsInitialized &&
               hasPendingDodge &&
               transactionId > 0 &&
               pendingDodge.Epoch == dodgeEpoch &&
               pendingDodge.Id == transactionId;
    }

    private long AllocateDodgeId()
    {
        if (nextDodgeId <= 0)
            nextDodgeId = 1;

        long allocated = nextDodgeId;
        nextDodgeId++;
        return allocated;
    }

    private void AdvanceDodgeEpoch()
    {
        dodgeEpoch++;
        if (dodgeEpoch <= 0)
            dodgeEpoch = 1;
    }

    private void ApplyNeutralPosesToTransforms()
    {
        ApplyPose(firstFighter.transform, firstNeutralPose);
        ApplyPose(secondFighter.transform, secondNeutralPose);
    }

    private bool NeutralTransformsMatch()
    {
        return TransformMatchesPose(
                   firstFighter.transform,
                   firstNeutralPose
               ) &&
               TransformMatchesPose(
                   secondFighter.transform,
                   secondNeutralPose
               );
    }

    private CombatSpatialSnapshot CreateSnapshot()
    {
        return new CombatSpatialSnapshot(
            revision,
            dodgeEpoch,
            IsInitialized,
            combatEnabled,
            firstFighter,
            secondFighter,
            advantageFighter,
            distanceLevel,
            relativeOrientation,
            IsInitialized ? GetHorizontalSeparation() : 0f,
            GetFlankAutoFaceRemaining(),
            firstMovement,
            secondMovement,
            firstNeutralPose,
            secondNeutralPose,
            hasPendingDodge,
            hasPendingDodge ? pendingDodge.Id : 0,
            GetOrientationDamageMultiplier(
                relativeOrientation
            ),
            CanApplyPermutation
        );
    }

    private void Publish(
        CombatSpatialChangeReason reason,
        FighterCombat instigator,
        long transactionId)
    {
        revision++;
        CombatSpatialSnapshot snapshot = CreateSnapshot();
        OnSnapshotChanged?.Invoke(snapshot);
        OnTelemetry?.Invoke(
            new CombatSpatialTelemetry(
                reason,
                snapshot,
                instigator,
                transactionId
            )
        );
    }

    private static Pose ReadPose(Transform target)
    {
        return new Pose(target.position, target.rotation);
    }

    private static Pose LerpPose(
        Pose from,
        Pose to,
        float progress)
    {
        return new Pose(
            Vector3.Lerp(from.position, to.position, progress),
            Quaternion.Slerp(
                from.rotation,
                to.rotation,
                progress
            )
        );
    }

    private static void ApplyPose(
        Transform target,
        Pose pose)
    {
        target.SetPositionAndRotation(
            pose.position,
            pose.rotation
        );
    }

    private static Quaternion FacingRotation(
        Vector3 from,
        Vector3 to,
        Quaternion fallback)
    {
        Vector3 direction = Horizontal(to - from);
        return direction.sqrMagnitude > PositionEpsilon
            ? Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            )
            : fallback;
    }

    private static bool TransformMatchesPose(
        Transform target,
        Pose pose)
    {
        return (target.position - pose.position).sqrMagnitude <=
               PositionEpsilon &&
               Quaternion.Angle(target.rotation, pose.rotation) <=
               RotationEpsilon;
    }

    private static bool PosesMatch(Pose first, Pose second)
    {
        return (first.position - second.position).sqrMagnitude <=
               PositionEpsilon &&
               Quaternion.Angle(first.rotation, second.rotation) <=
               RotationEpsilon;
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static bool IsStrafe(
        SpatialMovementType movement)
    {
        return movement == SpatialMovementType.StrafeLeft ||
               movement == SpatialMovementType.StrafeRight;
    }

    private static bool IsFlank(
        RelativeOrientation orientation)
    {
        return orientation == RelativeOrientation.LeftFlank ||
               orientation == RelativeOrientation.RightFlank;
    }

    private static bool IsKnownMovement(
        SpatialMovementType movement)
    {
        return movement >= SpatialMovementType.None &&
               movement <= SpatialMovementType.StrafeRight;
    }

    private static bool IsKnownDodgeDirection(
        DodgeDirection direction)
    {
        return direction >= DodgeDirection.Left &&
               direction <= DodgeDirection.Backward;
    }
}
