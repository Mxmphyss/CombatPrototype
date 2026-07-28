using System.Collections.Generic;

public enum GestureDebugPhase
{
    Started,
    Updated,
    Completed,
    Failed
}

public readonly struct GestureDebugEventData
{
    private static readonly int[] EmptyZones =
        System.Array.Empty<int>();

    public GestureDebugPhase Phase { get; }
    public GestureInputKind InputKind { get; }
    public GestureRecognitionStatus RecognitionStatus { get; }
    public CombatGestureId GestureId { get; }
    public IReadOnlyList<int> Zones { get; }
    public IReadOnlyList<int> RawZones { get; }
    public IReadOnlyList<int> NormalizedZones { get; }
    public bool IsActionMapped { get; }
    public bool HasCombatResult { get; }
    public CombatActionResult CombatResult { get; }
    public CombatRefusalReason RefusalReason { get; }
    public string ActionLabel { get; }
    public string CommandName => ActionLabel;

    public GestureDebugEventData(
        GestureDebugPhase phase,
        GestureInputKind inputKind,
        GestureRecognitionStatus recognitionStatus,
        CombatGestureId gestureId,
        IReadOnlyList<int> zones,
        bool isActionMapped,
        bool hasCombatResult,
        CombatActionResult combatResult,
        string actionLabel,
        CombatRefusalReason refusalReason =
            CombatRefusalReason.None,
        IReadOnlyList<int> rawZones = null,
        IReadOnlyList<int> normalizedZones = null)
    {
        Phase = phase;
        InputKind = inputKind;
        RecognitionStatus = recognitionStatus;
        GestureId = gestureId;
        Zones = zones ?? EmptyZones;
        RawZones = rawZones ?? Zones;
        NormalizedZones = normalizedZones ?? Zones;
        IsActionMapped = isActionMapped;
        HasCombatResult = hasCombatResult;
        CombatResult = combatResult;
        RefusalReason = refusalReason;
        ActionLabel = actionLabel ?? string.Empty;
    }

    public static GestureDebugEventData Tracking(
        GestureDebugPhase phase,
        IReadOnlyList<int> zones)
    {
        return new GestureDebugEventData(
            phase,
            GestureInputKind.Stroke,
            GestureRecognitionStatus.Invalid,
            CombatGestureId.None,
            zones,
            false,
            false,
            CombatActionResult.Unavailable,
            string.Empty
        );
    }
}
