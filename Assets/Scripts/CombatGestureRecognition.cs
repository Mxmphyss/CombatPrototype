using System;
using System.Collections.Generic;
using UnityEngine;

public enum GestureRecognitionStatus
{
    Recognized,
    Ambiguous,
    Invalid
}

public enum GestureInputKind
{
    Tap,
    Hold,
    Stroke,
    StrokeAndHold
}

public enum GestureDirection
{
    None,
    Left,
    Right,
    Up,
    Down
}

public enum GestureShape
{
    Point,
    HorizontalLine,
    VShape,
    Freeform
}

public enum CombatGestureId
{
    None,
    Tap,
    HeldGuard,
    StaminaCharge,
    DodgeRight,
    DodgeLeft,
    GrandV
}

[Serializable]
public sealed class HybridGestureRecognizerSettings
{
    [Header("Echantillonnage")]
    [Range(8, 64)]
    public int resampleCount = 32;

    [Min(0.001f)]
    public float minimumStrokeLength = 0.18f;

    [Min(0f)]
    public float minimumGestureDuration = 0.03f;

    [Min(0.05f)]
    public float maximumGestureDuration = 2.5f;

    [Header("Decision")]
    [Range(0f, 1f)]
    public float recognitionThreshold = 0.68f;

    [Range(0f, 0.5f)]
    public float ambiguityMargin = 0.08f;

    [Header("Lignes horizontales")]
    [Range(0.2f, 0.8f)]
    public float horizontalMinimumSpan = 0.46f;

    [Range(0.5f, 1f)]
    public float horizontalDirectionThreshold = 0.72f;

    [Range(0.2f, 0.6f)]
    public float bottomBandMaximum = 0.44f;

    [Range(0.05f, 0.35f)]
    public float bottomBandFalloff = 0.16f;

    [Header("Grand V")]
    [Range(0.4f, 0.9f)]
    public float vTopMinimum = 0.62f;

    [Range(0.1f, 0.6f)]
    public float vValleyMaximum = 0.42f;

    [Range(0.15f, 0.7f)]
    public float vMinimumDepth = 0.34f;

    [Range(0f, 0.4f)]
    public float vHorizontalTolerance = 0.24f;
}

public readonly struct TimedGestureSample
{
    public Vector2 Position { get; }
    public float Time { get; }

    public TimedGestureSample(Vector2 position, float time)
    {
        Position = position;
        Time = time;
    }
}

public readonly struct GestureRecognitionResult
{
    private static readonly int[] EmptyZones = Array.Empty<int>();

    public GestureRecognitionStatus Status { get; }
    public CombatGestureId GestureId { get; }
    public GestureInputKind InputKind { get; }
    public GestureDirection Direction { get; }
    public GestureShape Shape { get; }
    public IReadOnlyList<int> Zones { get; }
    public float Duration { get; }
    public float PathLength { get; }
    public float AverageSpeed { get; }
    public float Confidence { get; }

    public bool IsRecognized =>
        Status == GestureRecognitionStatus.Recognized;

    public GestureRecognitionResult(
        GestureRecognitionStatus status,
        CombatGestureId gestureId,
        GestureInputKind inputKind,
        GestureDirection direction,
        GestureShape shape,
        IReadOnlyList<int> zones,
        float duration,
        float pathLength,
        float confidence)
    {
        Status = status;
        GestureId = gestureId;
        InputKind = inputKind;
        Direction = direction;
        Shape = shape;
        Zones = zones ?? EmptyZones;
        Duration = Mathf.Max(0f, duration);
        PathLength = Mathf.Max(0f, pathLength);
        AverageSpeed = Duration > Mathf.Epsilon
            ? PathLength / Duration
            : 0f;
        Confidence = Mathf.Clamp01(confidence);
    }

    public static GestureRecognitionResult Invalid(
        float duration,
        float pathLength,
        IReadOnlyList<int> zones)
    {
        return new GestureRecognitionResult(
            GestureRecognitionStatus.Invalid,
            CombatGestureId.None,
            GestureInputKind.Stroke,
            GestureDirection.None,
            GestureShape.Freeform,
            zones,
            duration,
            pathLength,
            0f
        );
    }

    public GestureRecognitionResult WithInputKind(
        GestureInputKind inputKind)
    {
        return new GestureRecognitionResult(
            Status,
            GestureId,
            inputKind,
            Direction,
            Shape,
            Zones,
            Duration,
            PathLength,
            Confidence
        );
    }
}

public sealed class HybridGestureRecognizer
{
    private static readonly int[] DodgeRightZones = { 6, 7, 8 };
    private static readonly int[] DodgeLeftZones = { 8, 7, 6 };
    private static readonly int[] GrandVZones = { 0, 7, 2 };

    private readonly HybridGestureRecognizerSettings settings;
    private readonly float middleOuterOffset;
    private readonly List<Vector2> sourcePoints = new(64);
    private readonly List<Vector2> resampledPoints = new(64);
    private readonly List<int> projectedZones = new(9);
    private readonly List<RecognitionCandidate> candidates = new(3);

    public HybridGestureRecognizer(
        HybridGestureRecognizerSettings recognizerSettings,
        float middleZoneOuterOffset = 0.1f)
    {
        settings = recognizerSettings ??
            new HybridGestureRecognizerSettings();
        middleOuterOffset = Mathf.Clamp(
            middleZoneOuterOffset,
            0f,
            0.3f
        );
    }

    public GestureRecognitionResult Recognize(
        IReadOnlyList<TimedGestureSample> samples)
    {
        sourcePoints.Clear();
        resampledPoints.Clear();
        projectedZones.Clear();
        candidates.Clear();

        if (samples == null || samples.Count < 2)
            return GestureRecognitionResult.Invalid(0f, 0f, null);

        for (int index = 0; index < samples.Count; index++)
        {
            sourcePoints.Add(
                ClampNormalized(samples[index].Position)
            );
        }

        float duration = Mathf.Max(
            0f,
            samples[^1].Time - samples[0].Time
        );
        float pathLength = CalculatePathLength(sourcePoints);

        ProjectZones(
            sourcePoints,
            projectedZones,
            middleOuterOffset
        );

        if (pathLength < settings.minimumStrokeLength ||
            duration < settings.minimumGestureDuration ||
            duration > settings.maximumGestureDuration)
        {
            return GestureRecognitionResult.Invalid(
                duration,
                pathLength,
                projectedZones.ToArray()
            );
        }

        ResamplePath(
            sourcePoints,
            Mathf.Max(8, settings.resampleCount),
            resampledPoints
        );

        AddHorizontalCandidates();
        AddGrandVCandidate();
        candidates.Sort(
            static (left, right) =>
                right.Score.CompareTo(left.Score)
        );

        if (candidates.Count == 0 ||
            candidates[0].Score <
            settings.recognitionThreshold)
        {
            return GestureRecognitionResult.Invalid(
                duration,
                pathLength,
                projectedZones.ToArray()
            );
        }

        RecognitionCandidate best = candidates[0];
        if (candidates.Count > 1 &&
            best.Score - candidates[1].Score <
            settings.ambiguityMargin)
        {
            return new GestureRecognitionResult(
                GestureRecognitionStatus.Ambiguous,
                CombatGestureId.None,
                GestureInputKind.Stroke,
                GestureDirection.None,
                GestureShape.Freeform,
                projectedZones.ToArray(),
                duration,
                pathLength,
                best.Score
            );
        }

        return new GestureRecognitionResult(
            GestureRecognitionStatus.Recognized,
            best.GestureId,
            GestureInputKind.Stroke,
            best.Direction,
            best.Shape,
            best.Zones,
            duration,
            pathLength,
            best.Score
        );
    }

    public static int GetZone(Vector2 normalizedPosition)
    {
        return GetZone(normalizedPosition, 0.1f);
    }

    public static int GetZone(
        Vector2 normalizedPosition,
        float middleOuterOffset)
    {
        Vector2 position = ClampNormalized(normalizedPosition);
        int rowFromBottom = Mathf.Clamp(
            Mathf.FloorToInt(position.y * 3f),
            0,
            2
        );
        int rowFromTop = 2 - rowFromBottom;
        int column;

        if (rowFromTop == 1)
        {
            float boundaryOffset =
                Mathf.Clamp(
                    middleOuterOffset,
                    0f,
                    0.3f
                ) / 6f;
            float leftBoundary =
                1f / 3f - boundaryOffset;
            float rightBoundary =
                2f / 3f + boundaryOffset;
            column = position.x < leftBoundary
                ? 0
                : position.x > rightBoundary
                    ? 2
                    : 1;
        }
        else
        {
            column = Mathf.Clamp(
                Mathf.FloorToInt(position.x * 3f),
                0,
                2
            );
        }

        return rowFromTop * 3 + column;
    }

    public static Vector2 GetZoneCenter(
        int zone,
        float middleOuterOffset)
    {
        int safeZone = Mathf.Clamp(zone, 0, 8);
        int column = safeZone % 3;
        int row = safeZone / 3;
        float x = (column + 0.5f) / 3f;
        float y = 1f - (row + 0.5f) / 3f;

        if (safeZone == 3)
            x -= middleOuterOffset / 3f;
        else if (safeZone == 5)
            x += middleOuterOffset / 3f;

        return new Vector2(x, y);
    }

    private void AddHorizontalCandidates()
    {
        Vector2 start = resampledPoints[0];
        Vector2 end = resampledPoints[^1];
        Vector2 displacement = end - start;
        float horizontalSpan = Mathf.Abs(displacement.x);
        float axisTotal =
            horizontalSpan + Mathf.Abs(displacement.y);
        float horizontalRatio = axisTotal > Mathf.Epsilon
            ? horizontalSpan / axisTotal
            : 0f;
        float directionScore = Mathf.InverseLerp(
            settings.horizontalDirectionThreshold,
            1f,
            horizontalRatio
        );
        float spanScore = Mathf.Clamp01(
            horizontalSpan /
            settings.horizontalMinimumSpan
        );
        float bandScore = CalculateBottomBandScore();
        float straightnessScore = CalculateStraightness();
        float score = (
            directionScore * 0.38f +
            spanScore * 0.28f +
            bandScore * 0.2f +
            straightnessScore * 0.14f
        ) * bandScore;

        if (displacement.x > 0f)
        {
            candidates.Add(
                new RecognitionCandidate(
                    CombatGestureId.DodgeRight,
                    GestureDirection.Right,
                    GestureShape.HorizontalLine,
                    DodgeRightZones,
                    score
                )
            );
        }
        else if (displacement.x < 0f)
        {
            candidates.Add(
                new RecognitionCandidate(
                    CombatGestureId.DodgeLeft,
                    GestureDirection.Left,
                    GestureShape.HorizontalLine,
                    DodgeLeftZones,
                    score
                )
            );
        }
    }

    private void AddGrandVCandidate()
    {
        Vector2 start = resampledPoints[0];
        Vector2 end = resampledPoints[^1];
        int valleyIndex = 0;
        float valleyY = float.PositiveInfinity;

        for (int index = 1;
             index < resampledPoints.Count - 1;
             index++)
        {
            if (resampledPoints[index].y >= valleyY)
                continue;

            valleyIndex = index;
            valleyY = resampledPoints[index].y;
        }

        Vector2 valley = resampledPoints[valleyIndex];
        float startTopScore = Mathf.InverseLerp(
            settings.vTopMinimum,
            1f,
            start.y
        );
        float endTopScore = Mathf.InverseLerp(
            settings.vTopMinimum,
            1f,
            end.y
        );
        float valleyScore = 1f - Mathf.InverseLerp(
            settings.vValleyMaximum,
            1f,
            valley.y
        );
        float depth = Mathf.Min(start.y, end.y) - valley.y;
        float depthScore = Mathf.Clamp01(
            depth / settings.vMinimumDepth
        );
        float leftPositionScore = 1f - Mathf.InverseLerp(
            0.35f,
            0.5f,
            start.x
        );
        float rightPositionScore = Mathf.InverseLerp(
            0.5f,
            0.65f,
            end.x
        );
        float valleyCenterScore = 1f - Mathf.Clamp01(
            Mathf.Abs(valley.x - 0.5f) /
            Mathf.Max(0.01f, settings.vHorizontalTolerance)
        );
        float valleyProgress =
            valleyIndex /
            (float)(resampledPoints.Count - 1);
        float valleyTimingScore = 1f - Mathf.Clamp01(
            Mathf.Abs(valleyProgress - 0.5f) / 0.35f
        );
        float directionScore =
            start.x < valley.x && valley.x < end.x
                ? 1f
                : 0f;
        float score =
            startTopScore * 0.11f +
            endTopScore * 0.11f +
            valleyScore * 0.13f +
            depthScore * 0.19f +
            leftPositionScore * 0.1f +
            rightPositionScore * 0.1f +
            valleyCenterScore * 0.11f +
            valleyTimingScore * 0.07f +
            directionScore * 0.08f;

        candidates.Add(
            new RecognitionCandidate(
                CombatGestureId.GrandV,
                GestureDirection.None,
                GestureShape.VShape,
                GrandVZones,
                score
            )
        );
    }

    private float CalculateBottomBandScore()
    {
        float excessTotal = 0f;

        for (int index = 0;
             index < resampledPoints.Count;
             index++)
        {
            excessTotal += Mathf.Max(
                0f,
                resampledPoints[index].y -
                settings.bottomBandMaximum
            );
        }

        float averageExcess =
            excessTotal / resampledPoints.Count;
        return 1f - Mathf.Clamp01(
            averageExcess /
            Mathf.Max(0.01f, settings.bottomBandFalloff)
        );
    }

    private float CalculateStraightness()
    {
        float pathLength =
            CalculatePathLength(resampledPoints);
        if (pathLength <= Mathf.Epsilon)
            return 0f;

        float displacement = Vector2.Distance(
            resampledPoints[0],
            resampledPoints[^1]
        );
        return Mathf.Clamp01(displacement / pathLength);
    }

    private static void ProjectZones(
        IReadOnlyList<Vector2> positions,
        List<int> destination,
        float middleOuterOffset)
    {
        destination.Clear();

        for (int index = 0; index < positions.Count; index++)
        {
            int zone = GetZone(
                positions[index],
                middleOuterOffset
            );
            if (destination.Count == 0 ||
                destination[^1] != zone)
            {
                destination.Add(zone);
            }
        }
    }

    private static void ResamplePath(
        IReadOnlyList<Vector2> source,
        int targetCount,
        List<Vector2> destination)
    {
        destination.Clear();
        destination.Add(source[0]);

        float totalLength = CalculatePathLength(source);
        if (totalLength <= Mathf.Epsilon)
        {
            while (destination.Count < targetCount)
                destination.Add(source[0]);
            return;
        }

        float interval = totalLength / (targetCount - 1);
        float carriedDistance = 0f;
        Vector2 segmentStart = source[0];
        int sourceIndex = 1;

        while (sourceIndex < source.Count &&
               destination.Count < targetCount - 1)
        {
            Vector2 segmentEnd = source[sourceIndex];
            float segmentLength = Vector2.Distance(
                segmentStart,
                segmentEnd
            );

            if (segmentLength <= Mathf.Epsilon)
            {
                segmentStart = segmentEnd;
                sourceIndex++;
                continue;
            }

            if (carriedDistance + segmentLength >= interval)
            {
                float remaining = interval - carriedDistance;
                float progress = remaining / segmentLength;
                Vector2 sample = Vector2.Lerp(
                    segmentStart,
                    segmentEnd,
                    progress
                );
                destination.Add(sample);
                segmentStart = sample;
                carriedDistance = 0f;
            }
            else
            {
                carriedDistance += segmentLength;
                segmentStart = segmentEnd;
                sourceIndex++;
            }
        }

        while (destination.Count < targetCount)
            destination.Add(source[^1]);
    }

    private static float CalculatePathLength(
        IReadOnlyList<Vector2> positions)
    {
        float length = 0f;

        for (int index = 1; index < positions.Count; index++)
        {
            length += Vector2.Distance(
                positions[index - 1],
                positions[index]
            );
        }

        return length;
    }

    private static Vector2 ClampNormalized(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp01(position.x),
            Mathf.Clamp01(position.y)
        );
    }

    private readonly struct RecognitionCandidate
    {
        public CombatGestureId GestureId { get; }
        public GestureDirection Direction { get; }
        public GestureShape Shape { get; }
        public IReadOnlyList<int> Zones { get; }
        public float Score { get; }

        public RecognitionCandidate(
            CombatGestureId gestureId,
            GestureDirection direction,
            GestureShape shape,
            IReadOnlyList<int> zones,
            float score)
        {
            GestureId = gestureId;
            Direction = direction;
            Shape = shape;
            Zones = zones;
            Score = Mathf.Clamp01(score);
        }
    }
}
