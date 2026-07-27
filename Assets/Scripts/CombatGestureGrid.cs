using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CombatGestureGrid :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    private const int GridSize = 3;
    private const int MiddleDefensePoint = 4;
    private const int MiddleMovementPoint = 7;
    private const float HoldThreshold = 0.28f;

    private static readonly Color AttackColor =
        new(0.82f, 0.26f, 0.22f, 1f);
    private static readonly Color DefenseColor =
        new(0.24f, 0.55f, 0.78f, 1f);
    private static readonly Color MovementColor =
        new(0.78f, 0.66f, 0.25f, 1f);

    [Header("Detection tactile")]
    [SerializeField]
    [Min(1f)]
    private float initialPointDetectionRadius = 60f;

    [SerializeField]
    [Min(1f)]
    private float draggedPointDetectionRadius = 55f;

    [Header("Trace du geste")]
    [SerializeField]
    private bool traceEnabled = true;

    [SerializeField]
    [Min(1f)]
    private float traceLineWidth = 16f;

    [SerializeField]
    [Range(0f, 1f)]
    private float traceAlpha = 0.82f;

    private readonly List<int> gesture = new();
    private readonly List<Image> points = new();
    private readonly List<Image> segments = new();
    private readonly List<PointCandidate> pointCandidates =
        new(GridSize * GridSize);

    private FighterCombat fighter;
    private CombatHUD hud;
    private Camera activeEventCamera;
    private Pointer activePointerDevice;
    private int activePointerId = int.MinValue;
    private float pointerDownTime;
    private bool heldGuardStarted;
    private bool chargeStarted;
    private bool inputEnabled = true;
    private bool hasPointerLocalPosition;
    private Vector2 previousPointerLocalPosition;
    private Vector2 currentPointerLocalPosition;

    public static CombatGestureGrid Create(
        Transform parent,
        FighterCombat player,
        CombatHUD combatHud)
    {
        GameObject gridObject = new("Combat Gesture Grid");
        gridObject.transform.SetParent(parent, false);

        RectTransform rect = gridObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 205f);
        rect.sizeDelta = new Vector2(650f, 540f);

        Image surface = gridObject.AddComponent<Image>();
        surface.color = new Color(0.025f, 0.035f, 0.055f, 0.08f);
        surface.raycastTarget = true;

        Outline outline = gridObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.78f, 0.9f, 0.08f);
        outline.effectDistance = new Vector2(1f, -1f);

        CombatGestureGrid grid =
            gridObject.AddComponent<CombatGestureGrid>();
        grid.Initialize(player, combatHud);
        return grid;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
            CancelPointerAction();
    }

    private void Initialize(
        FighterCombat player,
        CombatHUD combatHud)
    {
        fighter = player;
        hud = combatHud;
        BuildTraceSegments();
        BuildPoints();
    }

    private void Update()
    {
        UpdateLiveTraceEndpoint();

        if (!inputEnabled ||
            activePointerId == int.MinValue ||
            gesture.Count != 1)
        {
            return;
        }

        if (heldGuardStarted)
        {
            PulsePoint(MiddleDefensePoint);
            return;
        }

        if (chargeStarted)
        {
            PulsePoint(MiddleMovementPoint);
            return;
        }

        int firstPoint = gesture[0];
        if (firstPoint is not MiddleDefensePoint and
            not MiddleMovementPoint)
        {
            return;
        }

        if (Time.unscaledTime - pointerDownTime < HoldThreshold)
            return;

        if (firstPoint == MiddleDefensePoint)
        {
            CombatActionResult guardResult =
                fighter.StartHeldGuard();
            heldGuardStarted =
                guardResult == CombatActionResult.Started;

            if (heldGuardStarted)
            {
                HighlightPoint(MiddleDefensePoint, 1f);
                ShowFeedback(
                    "Garde maintenue",
                    DefenseColor,
                    0.8f
                );
            }
            else
            {
                ShowActionResult(
                    guardResult,
                    "Garde maintenue",
                    DefenseColor
                );
            }

            return;
        }

        CombatActionResult chargeResult = fighter.StartCharge();
        chargeStarted =
            chargeResult == CombatActionResult.Started;

        if (chargeStarted)
        {
            HighlightPoint(MiddleMovementPoint, 1f);
            ShowFeedback(
                "Recharge endurance",
                MovementColor,
                0.8f
            );
        }
        else
        {
            ShowActionResult(
                chargeResult,
                "Recharge endurance",
                MovementColor
            );
        }
    }

    private void UpdateLiveTraceEndpoint()
    {
        if (!inputEnabled ||
            activePointerId == int.MinValue ||
            gesture.Count == 0)
        {
            return;
        }

        Pointer pointer =
            activePointerDevice ?? Pointer.current;
        if (pointer == null)
            return;

        Vector2 screenPosition =
            pointer.position.ReadValue();

        if (!TryGetPointerLocalPosition(
                screenPosition,
                activeEventCamera,
                out Vector2 localPosition))
        {
            return;
        }

        currentPointerLocalPosition = localPosition;
        UpdateTraceVisual();
    }

    private void OnDisable()
    {
        CancelPointerAction();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!inputEnabled ||
            activePointerId != int.MinValue ||
            fighter == null ||
            fighter.IsDead)
        {
            return;
        }

        activePointerId = eventData.pointerId;
        activeEventCamera = eventData.pressEventCamera;
        activePointerDevice = Pointer.current;
        pointerDownTime = Time.unscaledTime;
        heldGuardStarted = false;
        chargeStarted = false;
        ClearGesture();
        TrackPointer(
            eventData.position,
            eventData.pressEventCamera
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!inputEnabled ||
            eventData.pointerId != activePointerId ||
            heldGuardStarted ||
            chargeStarted)
        {
            return;
        }

        TrackPointer(
            eventData.position,
            eventData.pressEventCamera
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        if (!heldGuardStarted &&
            !chargeStarted &&
            inputEnabled)
        {
            TrackPointer(
                eventData.position,
                eventData.pressEventCamera
            );
        }

        if (heldGuardStarted)
        {
            fighter.StopHeldGuard();
            ShowFeedback(
                "Garde relachee",
                DefenseColor,
                0.8f
            );
        }
        else if (chargeStarted)
        {
            fighter.StopChargeInput();
            ShowFeedback(
                "Recharge arretee",
                MovementColor,
                0.8f
            );
        }
        else if (inputEnabled)
        {
            ExecuteGesture();
        }

        ResetPointerState();
    }

    private void BuildTraceSegments()
    {
        for (int index = 0;
             index < GridSize * GridSize;
             index++)
        {
            GameObject segmentObject =
                new($"Gesture Trace Segment {index}");
            segmentObject.transform.SetParent(transform, false);

            RectTransform segmentRect =
                segmentObject.AddComponent<RectTransform>();
            segmentRect.anchorMin = segmentRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            segmentRect.sizeDelta = Vector2.zero;

            Image segment = segmentObject.AddComponent<Image>();
            segment.raycastTarget = false;
            segmentObject.SetActive(false);
            segments.Add(segment);
        }
    }

    private void BuildPoints()
    {
        const float horizontalSpacing = 170f;
        const float verticalSpacing = 155f;
        const float pointSize = 54f;

        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                int index = row * GridSize + column;
                GameObject pointObject =
                    new($"Gesture Point {index}");
                pointObject.transform.SetParent(transform, false);

                RectTransform pointRect =
                    pointObject.AddComponent<RectTransform>();
                pointRect.anchorMin = pointRect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                pointRect.sizeDelta =
                    new Vector2(pointSize, pointSize);
                pointRect.anchoredPosition = new Vector2(
                    (column - 1) * horizontalSpacing,
                    (1 - row) * verticalSpacing
                );

                Image point = pointObject.AddComponent<Image>();
                point.color = RestingColor(index);
                point.raycastTarget = false;
                points.Add(point);

                AddPointLabel(pointObject.transform, index);
            }
        }
    }

    private static void AddPointLabel(
        Transform parent,
        int index)
    {
        GameObject labelObject =
            new($"Gesture Label {(char)('A' + index)}");
        labelObject.transform.SetParent(parent, false);

        RectTransform rect =
            labelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        label.alignment = TextAnchor.MiddleCenter;
        label.fontStyle = FontStyle.Bold;
        label.fontSize = 24;
        label.color = new Color(1f, 1f, 1f, 0.88f);
        label.raycastTarget = false;
        label.text = ((char)('A' + index)).ToString();
    }

    private void TrackPointer(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (!TryGetPointerLocalPosition(
                screenPosition,
                eventCamera,
                out Vector2 localPosition))
        {
            return;
        }

        currentPointerLocalPosition = localPosition;

        if (!hasPointerLocalPosition)
        {
            previousPointerLocalPosition = localPosition;
            hasPointerLocalPosition = true;
        }

        SelectPointsAlongPointerSegment(
            previousPointerLocalPosition,
            currentPointerLocalPosition
        );

        previousPointerLocalPosition =
            currentPointerLocalPosition;
        UpdateTraceVisual();
    }

    private bool TryGetPointerLocalPosition(
        Vector2 screenPosition,
        Camera eventCamera,
        out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform,
            screenPosition,
            eventCamera,
            out localPosition
        );
    }

    private void SelectPointsAlongPointerSegment(
        Vector2 from,
        Vector2 to)
    {
        float minimumProgress = 0f;

        if (gesture.Count == 0)
        {
            if (!TryFindInitialPoint(
                    from,
                    to,
                    out int initialPoint,
                    out minimumProgress))
            {
                return;
            }

            SelectPoint(initialPoint);
        }

        CollectDraggedPointCandidates(
            from,
            to,
            minimumProgress
        );

        pointCandidates.Sort(
            static (left, right) =>
            {
                int progressComparison =
                    left.Progress.CompareTo(right.Progress);
                return progressComparison != 0
                    ? progressComparison
                    : left.DistanceSquared.CompareTo(
                        right.DistanceSquared
                    );
            }
        );

        for (int index = 0;
             index < pointCandidates.Count;
             index++)
        {
            SelectPoint(pointCandidates[index].Index);
        }
    }

    private bool TryFindInitialPoint(
        Vector2 from,
        Vector2 to,
        out int selectedPoint,
        out float selectedProgress)
    {
        selectedPoint = -1;
        selectedProgress = float.PositiveInfinity;
        float selectedDistanceSquared = float.PositiveInfinity;
        float radiusSquared =
            initialPointDetectionRadius *
            initialPointDetectionRadius;

        for (int index = 0; index < points.Count; index++)
        {
            float distanceSquared = DistanceSquaredToSegment(
                GetPointLocalPosition(index),
                from,
                to,
                out float progress
            );

            if (distanceSquared > radiusSquared ||
                progress > selectedProgress ||
                (Mathf.Approximately(
                     progress,
                     selectedProgress) &&
                 distanceSquared >= selectedDistanceSquared))
            {
                continue;
            }

            selectedPoint = index;
            selectedProgress = progress;
            selectedDistanceSquared = distanceSquared;
        }

        if (selectedPoint >= 0)
            return true;

        selectedProgress = 0f;
        return false;
    }

    private void CollectDraggedPointCandidates(
        Vector2 from,
        Vector2 to,
        float minimumProgress)
    {
        pointCandidates.Clear();
        float radiusSquared =
            draggedPointDetectionRadius *
            draggedPointDetectionRadius;

        for (int index = 0; index < points.Count; index++)
        {
            if (gesture.Contains(index))
                continue;

            float distanceSquared = DistanceSquaredToSegment(
                GetPointLocalPosition(index),
                from,
                to,
                out float progress
            );

            if (progress + Mathf.Epsilon < minimumProgress ||
                distanceSquared > radiusSquared)
            {
                continue;
            }

            pointCandidates.Add(
                new PointCandidate(
                    index,
                    progress,
                    distanceSquared
                )
            );
        }
    }

    private void SelectPoint(int targetIndex)
    {
        if (targetIndex < 0 ||
            targetIndex >= points.Count ||
            gesture.Contains(targetIndex))
        {
            return;
        }

        if (gesture.Count > 0)
        {
            AddIntermediatePoints(
                gesture[^1],
                targetIndex
            );
        }

        AppendPoint(targetIndex);
    }

    private void AddIntermediatePoints(
        int fromIndex,
        int toIndex)
    {
        int fromX = fromIndex % GridSize;
        int fromY = fromIndex / GridSize;
        int toX = toIndex % GridSize;
        int toY = toIndex / GridSize;
        int differenceX = toX - fromX;
        int differenceY = toY - fromY;
        int stepCount = GreatestCommonDivisor(
            Mathf.Abs(differenceX),
            Mathf.Abs(differenceY)
        );

        if (stepCount <= 1)
            return;

        int stepX = differenceX / stepCount;
        int stepY = differenceY / stepCount;

        for (int step = 1; step < stepCount; step++)
        {
            int intermediateX = fromX + stepX * step;
            int intermediateY = fromY + stepY * step;
            AppendPoint(
                intermediateY * GridSize + intermediateX
            );
        }
    }

    private void AppendPoint(int pointIndex)
    {
        if (gesture.Contains(pointIndex))
            return;

        gesture.Add(pointIndex);
        HighlightPoint(pointIndex, 1f);
        ShowFeedback(
            FormatGesture(),
            points[gesture[0]].color,
            0.7f
        );
        UpdateTraceVisual();
    }

    private static int GreatestCommonDivisor(
        int left,
        int right)
    {
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    private Vector2 GetPointLocalPosition(int pointIndex)
    {
        RectTransform gridRect = (RectTransform)transform;
        return gridRect.InverseTransformPoint(
            points[pointIndex].rectTransform.position
        );
    }

    private static float DistanceSquaredToSegment(
        Vector2 point,
        Vector2 from,
        Vector2 to,
        out float progress)
    {
        Vector2 segment = to - from;
        float squaredLength = segment.sqrMagnitude;

        if (squaredLength <= Mathf.Epsilon)
        {
            progress = 0f;
            return (point - from).sqrMagnitude;
        }

        progress = Mathf.Clamp01(
            Vector2.Dot(point - from, segment) /
            squaredLength
        );
        Vector2 closestPoint = from + segment * progress;
        return (point - closestPoint).sqrMagnitude;
    }

    private void UpdateTraceVisual()
    {
        if (!traceEnabled ||
            gesture.Count == 0 ||
            !hasPointerLocalPosition)
        {
            HideTraceSegments();
            return;
        }

        Color traceColor = points[gesture[0]].color;
        traceColor.a = traceAlpha;
        int segmentIndex = 0;

        for (int index = 1;
             index < gesture.Count;
             index++)
        {
            ConfigureTraceSegment(
                segmentIndex++,
                GetPointLocalPosition(gesture[index - 1]),
                GetPointLocalPosition(gesture[index]),
                traceColor
            );
        }

        ConfigureTraceSegment(
            segmentIndex++,
            GetPointLocalPosition(gesture[^1]),
            currentPointerLocalPosition,
            traceColor
        );

        for (int index = segmentIndex;
             index < segments.Count;
             index++)
        {
            segments[index].gameObject.SetActive(false);
        }
    }

    private void ConfigureTraceSegment(
        int segmentIndex,
        Vector2 from,
        Vector2 to,
        Color color)
    {
        if (segmentIndex < 0 ||
            segmentIndex >= segments.Count)
        {
            return;
        }

        Image segment = segments[segmentIndex];
        RectTransform segmentRect = segment.rectTransform;
        Vector2 direction = to - from;

        segment.color = color;
        segmentRect.localPosition = (from + to) * 0.5f;
        segmentRect.sizeDelta = new Vector2(
            direction.magnitude,
            traceLineWidth
        );
        segmentRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg
        );
        segment.gameObject.SetActive(true);
    }

    private void HideTraceSegments()
    {
        for (int index = 0; index < segments.Count; index++)
            segments[index].gameObject.SetActive(false);
    }

    private void ExecuteGesture()
    {
        if (gesture.Count == 0)
            return;

        if (gesture.Count == 1)
        {
            ExecuteTap(gesture[0]);
            return;
        }

        if (Matches(6, 7, 8))
        {
            ShowActionResult(
                fighter.DodgeRight(),
                "Esquive droite",
                MovementColor
            );
        }
        else if (Matches(8, 7, 6))
        {
            ShowActionResult(
                fighter.DodgeLeft(),
                "Esquive gauche",
                MovementColor
            );
        }
        else
        {
            ShowFeedback(
                "Commande inconnue",
                Color.white,
                1f
            );
        }
    }

    private void ExecuteTap(int point)
    {
        if (point is >= 0 and <= 2)
        {
            ShowActionResult(
                fighter.LightAttack(),
                "Attaque legere",
                AttackColor
            );
        }
        else if (point is >= 3 and <= 5)
        {
            ShowActionResult(
                fighter.StartDefense(),
                "Defense simple",
                DefenseColor
            );
        }
        else
        {
            ShowFeedback(
                "Commande inconnue",
                Color.white,
                1f
            );
        }
    }

    private bool Matches(params int[] expected)
    {
        if (gesture.Count != expected.Length)
            return false;

        for (int index = 0; index < expected.Length; index++)
        {
            if (gesture[index] != expected[index])
                return false;
        }

        return true;
    }

    private void CancelPointerAction()
    {
        if (heldGuardStarted && fighter != null)
            fighter.StopHeldGuard();

        if (chargeStarted && fighter != null)
            fighter.StopChargeInput();

        ResetPointerState();
    }

    private void ResetPointerState()
    {
        activePointerId = int.MinValue;
        activeEventCamera = null;
        activePointerDevice = null;
        heldGuardStarted = false;
        chargeStarted = false;
        hasPointerLocalPosition = false;
        previousPointerLocalPosition = Vector2.zero;
        currentPointerLocalPosition = Vector2.zero;
        ClearGesture();
    }

    private void ClearGesture()
    {
        gesture.Clear();
        pointCandidates.Clear();
        HideTraceSegments();

        for (int index = 0; index < points.Count; index++)
        {
            points[index].color = RestingColor(index);
            points[index].rectTransform.localScale =
                Vector3.one;
        }
    }

    private void HighlightPoint(int index, float alpha)
    {
        Color color = RowColor(index);
        color.a = alpha;
        points[index].color = color;
    }

    private void PulsePoint(int index)
    {
        float pulse =
            1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.07f;
        points[index].rectTransform.localScale =
            Vector3.one * pulse;
    }

    private string FormatGesture()
    {
        if (gesture.Count == 0)
            return string.Empty;

        char[] labels = new char[gesture.Count];
        for (int index = 0; index < gesture.Count; index++)
            labels[index] = (char)('A' + gesture[index]);

        return string.Join(" -> ", labels);
    }

    private void ShowFeedback(
        string message,
        Color color,
        float duration)
    {
        hud?.ShowMessage(message, color, duration);
    }

    private void ShowActionResult(
        CombatActionResult result,
        string successMessage,
        Color successColor)
    {
        switch (result)
        {
            case CombatActionResult.Started:
                ShowFeedback(
                    successMessage,
                    successColor,
                    1f
                );
                break;

            case CombatActionResult.NotEnoughStamina:
                ShowFeedback(
                    "Endurance insuffisante",
                    Color.white,
                    1.2f
                );
                break;

            case CombatActionResult.Busy:
                ShowFeedback(
                    "Action en cours",
                    Color.white,
                    1f
                );
                break;

            default:
                ShowFeedback(
                    "Combat termine",
                    Color.white,
                    1f
                );
                break;
        }
    }

    private readonly struct PointCandidate
    {
        public int Index { get; }
        public float Progress { get; }
        public float DistanceSquared { get; }

        public PointCandidate(
            int index,
            float progress,
            float distanceSquared)
        {
            Index = index;
            Progress = progress;
            DistanceSquared = distanceSquared;
        }
    }

    private static Color RestingColor(int index)
    {
        Color color = RowColor(index);
        color.a = 0.24f;
        return color;
    }

    private static Color RowColor(int index)
    {
        if (index <= 2)
            return AttackColor;
        if (index <= 5)
            return DefenseColor;
        return MovementColor;
    }
}
