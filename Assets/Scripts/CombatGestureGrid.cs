using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CombatGestureGrid :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    ICancelHandler
{
    private const int GridSize = 3;
    private const int MiddleDefenseZone = 4;
    private const int MiddleMovementZone = 7;

    [Header("Couleurs des categories")]
    [SerializeField]
    private Color attackColor =
        new(0.82f, 0.26f, 0.22f, 1f);

    [SerializeField]
    private Color defenseColor =
        new(0.24f, 0.55f, 0.78f, 1f);

    [SerializeField]
    private Color movementColor =
        new(0.78f, 0.66f, 0.25f, 1f);

    [Header("Surface tactile")]
    [SerializeField]
    private Vector2 padSize = new(650f, 540f);

    [SerializeField]
    private Vector2 padAnchoredPosition = new(0f, 205f);

    [Range(0f, 0.3f)]
    [SerializeField]
    private float middleOuterOffset = 0.1f;

    [Header("Appui et maintien")]
    [Min(0.05f)]
    [SerializeField]
    private float holdThreshold = 0.28f;

    [Range(0.005f, 0.2f)]
    [SerializeField]
    private float tapMovementThreshold = 0.065f;

    [Range(0.005f, 0.2f)]
    [SerializeField]
    private float holdMovementTolerance = 0.05f;

    [Header("Echantillonnage")]
    [Range(0.001f, 0.05f)]
    [SerializeField]
    private float sampleSpacingNormalized = 0.008f;

    [Range(32, 512)]
    [SerializeField]
    private int maximumRecordedSamples = 256;

    [Header("Ruban lumineux")]
    [SerializeField]
    private bool traceEnabled = true;

    [Min(1f)]
    [SerializeField]
    private float traceLineWidth = 64f;

    [Range(0f, 1f)]
    [SerializeField]
    private float traceAlpha = 0.58f;

    [Range(4, 20)]
    [SerializeField]
    private int traceRoundSegments = 10;

    [Header("Reconnaissance")]
    [SerializeField]
    private HybridGestureRecognizerSettings recognition =
        new();

    [Header("Confirmation visuelle")]
    [SerializeField]
    private bool recognitionFeedbackEnabled = true;

    [Range(0.05f, 1f)]
    [SerializeField]
    private float recognitionFeedbackDuration = 0.22f;

    private readonly List<Image> points =
        new(GridSize * GridSize);
    private readonly List<Vector2> traceLocalSamples = new(256);
    private readonly List<TimedGestureSample>
        normalizedSamples = new(256);

    private CombatHUD hud;
    private CombatGestureCommandRouter commandRouter;
    private HybridGestureRecognizer gestureRecognizer;
    private GestureRibbonGraphic ribbon;
    private Camera activeEventCamera;
    private Pointer activePointerDevice;
    private Coroutine recognitionFeedbackRoutine;
    private int activePointerId = int.MinValue;
    private int startingZone = -1;
    private int activeHoldZone = -1;
    private float pointerDownTime;
    private float lastMeaningfulMovementTime;
    private float maximumDisplacementSquared;
    private bool inputEnabled = true;
    private bool holdAttempted;
    private bool holdStarted;
    private bool strokeFollowedByHold;
    private bool hasPointerLocalPosition;
    private Vector2 startingNormalizedPosition;
    private Vector2 lastMovementAnchorNormalized;
    private Vector2 currentPointerLocalPosition;
    private Vector2 currentNormalizedPosition;

    public static CombatGestureGrid Create(
        Transform parent,
        FighterCombat player,
        CombatHUD combatHud)
    {
        GameObject gridObject = new("Combat Gesture Grid");
        gridObject.transform.SetParent(parent, false);

        RectTransform rect =
            gridObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);

        Image surface = gridObject.AddComponent<Image>();
        surface.color =
            new Color(0.025f, 0.035f, 0.055f, 0.08f);
        surface.raycastTarget = true;

        Outline outline = gridObject.AddComponent<Outline>();
        outline.effectColor =
            new Color(0.7f, 0.78f, 0.9f, 0.08f);
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
        {
            CancelPointerAction();
            CancelRecognitionFeedback();
        }
    }

    private void Initialize(
        FighterCombat player,
        CombatHUD combatHud)
    {
        hud = combatHud;
        commandRouter =
            new CombatGestureCommandRouter(player);
        gestureRecognizer =
            new HybridGestureRecognizer(
                recognition,
                middleOuterOffset
            );

        RectTransform rect = (RectTransform)transform;
        rect.anchoredPosition = padAnchoredPosition;
        rect.sizeDelta = padSize;

        BuildRibbon();
        BuildPoints();
        ResetPointerState();
    }

    private void Update()
    {
        if (activePointerId != int.MinValue &&
            commandRouter != null &&
            commandRouter.ShouldCancelInput)
        {
            CancelPointerAction();
            return;
        }

        UpdateLivePointer();
        UpdateHoldRecognition();
    }

    private void UpdateLivePointer()
    {
        if (!inputEnabled ||
            activePointerId == int.MinValue)
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

        UpdatePointerPosition(localPosition, false);
    }

    private void UpdateHoldRecognition()
    {
        if (!inputEnabled ||
            activePointerId == int.MinValue ||
            holdAttempted ||
            holdStarted ||
            !hasPointerLocalPosition ||
            maximumDisplacementSquared >
            holdMovementTolerance * holdMovementTolerance)
        {
            if (holdStarted)
                PulsePoint(activeHoldZone);
            return;
        }

        if (Time.unscaledTime - pointerDownTime <
            holdThreshold)
        {
            return;
        }

        if (startingZone is not MiddleDefenseZone and
            not MiddleMovementZone)
        {
            return;
        }

        holdAttempted = true;
        RoutedGestureAction action =
            commandRouter.BeginHold(startingZone);

        if (!action.IsMapped)
            return;

        holdStarted =
            action.CombatResult == CombatActionResult.Started;
        activeHoldZone = startingZone;
        PresentAction(action);

        if (holdStarted)
            HighlightPoint(activeHoldZone, 1f);
    }

    private void OnDisable()
    {
        CancelPointerAction();
        CancelRecognitionFeedback();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelPointerAction();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelPointerAction();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!inputEnabled ||
            activePointerId != int.MinValue ||
            commandRouter == null ||
            commandRouter.IsDead)
        {
            return;
        }

        CancelRecognitionFeedback();
        ResetPointVisuals();
        ClearRecordedGesture();

        activePointerId = eventData.pointerId;
        activeEventCamera = eventData.pressEventCamera;
        activePointerDevice = Pointer.current;
        pointerDownTime = Time.unscaledTime;
        lastMeaningfulMovementTime = pointerDownTime;
        holdAttempted = false;
        holdStarted = false;
        strokeFollowedByHold = false;
        activeHoldZone = -1;

        if (!TryGetPointerLocalPosition(
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition))
        {
            ResetPointerState();
            return;
        }

        currentPointerLocalPosition = localPosition;
        currentNormalizedPosition =
            LocalToNormalized(localPosition);
        startingNormalizedPosition =
            currentNormalizedPosition;
        lastMovementAnchorNormalized =
            currentNormalizedPosition;
        startingZone = HybridGestureRecognizer.GetZone(
            startingNormalizedPosition,
            middleOuterOffset
        );
        hasPointerLocalPosition = true;
        AddGestureSample(
            localPosition,
            currentNormalizedPosition,
            true
        );
        HighlightPoint(startingZone, 1f);
        UpdateRibbon();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!inputEnabled ||
            eventData.pointerId != activePointerId)
        {
            return;
        }

        if (!TryGetPointerLocalPosition(
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition))
        {
            return;
        }

        UpdatePointerPosition(localPosition, false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        if (TryGetPointerLocalPosition(
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition))
        {
            UpdatePointerPosition(localPosition, true);
        }

        if (holdStarted)
        {
            commandRouter.EndHold(activeHoldZone);
            ShowFeedback(
                activeHoldZone == MiddleDefenseZone
                    ? "Garde relachee"
                    : "Recharge arretee",
                ZoneColor(activeHoldZone),
                0.8f
            );
            ResetPointerState();
            return;
        }

        bool isTap =
            maximumDisplacementSquared <=
            tapMovementThreshold * tapMovementThreshold;

        if (isTap)
        {
            int tapZone = startingZone;
            ResetPointerState();
            RoutedGestureAction tapAction =
                commandRouter.ExecuteTap(tapZone);
            PresentAction(tapAction);
            StartRecognitionFeedback(
                new[] { tapZone },
                ZoneColor(tapZone)
            );
            return;
        }

        GestureRecognitionResult result =
            gestureRecognizer.Recognize(normalizedSamples);

        if (strokeFollowedByHold)
        {
            result = result.WithInputKind(
                GestureInputKind.StrokeAndHold
            );
        }

        ResetPointerState();
        PresentRecognition(result);
    }

    public void OnCancel(BaseEventData eventData)
    {
        CancelPointerAction();
    }

    private void UpdatePointerPosition(
        Vector2 localPosition,
        bool forceSample)
    {
        currentPointerLocalPosition = localPosition;
        currentNormalizedPosition =
            LocalToNormalized(localPosition);
        hasPointerLocalPosition = true;

        float displacementSquared =
            (currentNormalizedPosition -
             startingNormalizedPosition).sqrMagnitude;
        maximumDisplacementSquared = Mathf.Max(
            maximumDisplacementSquared,
            displacementSquared
        );
        UpdateStrokeHoldState();

        if (!holdStarted)
        {
            AddGestureSample(
                localPosition,
                currentNormalizedPosition,
                forceSample
            );
        }

        UpdateRibbon();
    }

    private void UpdateStrokeHoldState()
    {
        float toleranceSquared =
            holdMovementTolerance *
            holdMovementTolerance;

        if ((currentNormalizedPosition -
             lastMovementAnchorNormalized).sqrMagnitude >
            toleranceSquared)
        {
            lastMovementAnchorNormalized =
                currentNormalizedPosition;
            lastMeaningfulMovementTime =
                Time.unscaledTime;
            strokeFollowedByHold = false;
            return;
        }

        if (maximumDisplacementSquared >
                tapMovementThreshold *
                tapMovementThreshold &&
            Time.unscaledTime -
                lastMeaningfulMovementTime >=
                holdThreshold)
        {
            strokeFollowedByHold = true;
        }
    }

    private void AddGestureSample(
        Vector2 localPosition,
        Vector2 normalizedPosition,
        bool force)
    {
        if (!force &&
            normalizedSamples.Count > 0 &&
            (normalizedSamples[^1].Position -
             normalizedPosition).sqrMagnitude <
            sampleSpacingNormalized *
            sampleSpacingNormalized)
        {
            return;
        }

        if (normalizedSamples.Count >=
            Mathf.Max(32, maximumRecordedSamples))
        {
            CompactSamples();
        }

        traceLocalSamples.Add(localPosition);
        normalizedSamples.Add(
            new TimedGestureSample(
                normalizedPosition,
                Time.unscaledTime
            )
        );
    }

    private void CompactSamples()
    {
        int writeIndex = 1;

        for (int readIndex = 2;
             readIndex < normalizedSamples.Count - 1;
             readIndex += 2)
        {
            normalizedSamples[writeIndex] =
                normalizedSamples[readIndex];
            traceLocalSamples[writeIndex] =
                traceLocalSamples[readIndex];
            writeIndex++;
        }

        normalizedSamples.RemoveRange(
            writeIndex,
            normalizedSamples.Count - writeIndex
        );
        traceLocalSamples.RemoveRange(
            writeIndex,
            traceLocalSamples.Count - writeIndex
        );
    }

    private void BuildRibbon()
    {
        GameObject ribbonObject =
            new("Gesture Ribbon");
        ribbonObject.transform.SetParent(transform, false);

        RectTransform gridRect = (RectTransform)transform;
        RectTransform ribbonRect =
            ribbonObject.AddComponent<RectTransform>();
        ribbonRect.anchorMin = Vector2.zero;
        ribbonRect.anchorMax = Vector2.one;
        ribbonRect.pivot = gridRect.pivot;
        ribbonRect.offsetMin = Vector2.zero;
        ribbonRect.offsetMax = Vector2.zero;

        ribbon =
            ribbonObject.AddComponent<GestureRibbonGraphic>();
        ribbon.raycastTarget = false;
        ribbon.ClearPath();
    }

    private void BuildPoints()
    {
        const float pointSize = 54f;

        for (int index = 0;
             index < GridSize * GridSize;
             index++)
        {
            GameObject pointObject =
                new($"Gesture Point {index}");
            pointObject.transform.SetParent(transform, false);

            RectTransform pointRect =
                pointObject.AddComponent<RectTransform>();
            pointRect.anchorMin = pointRect.anchorMax =
                Vector2.zero;
            pointRect.pivot = new Vector2(0.5f, 0.5f);
            pointRect.sizeDelta =
                new Vector2(pointSize, pointSize);
            pointRect.localPosition = NormalizedToLocal(
                HybridGestureRecognizer.GetZoneCenter(
                    index,
                    middleOuterOffset
                )
            );

            Image point = pointObject.AddComponent<Image>();
            point.color = RestingColor(index);
            point.raycastTarget = false;
            points.Add(point);

            AddPointLabel(pointObject.transform, index);
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
        label.color =
            new Color(1f, 1f, 1f, 0.88f);
        label.raycastTarget = false;
        label.text = ((char)('A' + index)).ToString();
    }

    private void UpdateRibbon()
    {
        if (ribbon == null)
            return;

        if (!traceEnabled ||
            !hasPointerLocalPosition ||
            traceLocalSamples.Count == 0)
        {
            ribbon.ClearPath();
            return;
        }

        Color traceColor = ZoneColor(startingZone);
        traceColor.a = traceAlpha;
        ribbon.SetPath(
            traceLocalSamples,
            currentPointerLocalPosition,
            traceLineWidth,
            traceColor,
            traceRoundSegments
        );
    }

    private void PresentRecognition(
        GestureRecognitionResult recognitionResult)
    {
        Color color = recognitionResult.Zones.Count > 0
            ? ZoneColor(recognitionResult.Zones[0])
            : Color.white;

        switch (recognitionResult.Status)
        {
            case GestureRecognitionStatus.Recognized:
                RoutedGestureAction action =
                    commandRouter.ExecuteStroke(
                        recognitionResult
                    );
                PresentAction(action);
                StartRecognitionFeedback(
                    recognitionResult.Zones,
                    color
                );
                break;

            case GestureRecognitionStatus.Ambiguous:
                ShowFeedback(
                    "Geste ambigu",
                    Color.white,
                    1f
                );
                break;

            default:
                ShowFeedback(
                    "Geste invalide",
                    Color.white,
                    1f
                );
                break;
        }
    }

    private void PresentAction(RoutedGestureAction action)
    {
        Color color = ZoneColor(action.CategoryZone);

        if (!action.IsMapped ||
            !action.HasCombatResult)
        {
            ShowFeedback(
                action.Label,
                color,
                1f
            );
            return;
        }

        ShowActionResult(
            action.CombatResult,
            action.Label,
            color
        );
    }

    private void StartRecognitionFeedback(
        IReadOnlyList<int> zones,
        Color color)
    {
        if (!recognitionFeedbackEnabled ||
            zones == null ||
            zones.Count == 0)
        {
            return;
        }

        CancelRecognitionFeedback();
        recognitionFeedbackRoutine = StartCoroutine(
            RecognitionFeedbackRoutine(zones, color)
        );
    }

    private IEnumerator RecognitionFeedbackRoutine(
        IReadOnlyList<int> zones,
        Color color)
    {
        for (int index = 0; index < zones.Count; index++)
        {
            int zone = zones[index];
            if (zone < 0 || zone >= points.Count)
                continue;

            Color highlighted = color;
            highlighted.a = 1f;
            points[zone].color = highlighted;
            points[zone].rectTransform.localScale =
                Vector3.one * 1.12f;
        }

        float elapsed = 0f;
        while (elapsed < recognitionFeedbackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        recognitionFeedbackRoutine = null;
        ResetPointVisuals();
    }

    private void CancelRecognitionFeedback()
    {
        if (recognitionFeedbackRoutine != null)
        {
            StopCoroutine(recognitionFeedbackRoutine);
            recognitionFeedbackRoutine = null;
        }

        ResetPointVisuals();
    }

    private void CancelPointerAction()
    {
        if (holdStarted &&
            commandRouter != null &&
            activeHoldZone >= 0)
        {
            commandRouter.EndHold(activeHoldZone);
        }

        ResetPointerState();
    }

    private void ResetPointerState()
    {
        activePointerId = int.MinValue;
        activeEventCamera = null;
        activePointerDevice = null;
        startingZone = -1;
        activeHoldZone = -1;
        pointerDownTime = 0f;
        lastMeaningfulMovementTime = 0f;
        maximumDisplacementSquared = 0f;
        holdAttempted = false;
        holdStarted = false;
        strokeFollowedByHold = false;
        hasPointerLocalPosition = false;
        startingNormalizedPosition = Vector2.zero;
        lastMovementAnchorNormalized = Vector2.zero;
        currentNormalizedPosition = Vector2.zero;
        currentPointerLocalPosition = Vector2.zero;
        ClearRecordedGesture();

        if (ribbon != null)
            ribbon.ClearPath();

        ResetPointVisuals();
    }

    private void ClearRecordedGesture()
    {
        traceLocalSamples.Clear();
        normalizedSamples.Clear();
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

    private Vector2 LocalToNormalized(Vector2 localPosition)
    {
        Rect rect = ((RectTransform)transform).rect;
        return new Vector2(
            Mathf.InverseLerp(
                rect.xMin,
                rect.xMax,
                localPosition.x
            ),
            Mathf.InverseLerp(
                rect.yMin,
                rect.yMax,
                localPosition.y
            )
        );
    }

    private Vector2 NormalizedToLocal(
        Vector2 normalizedPosition)
    {
        Rect rect = ((RectTransform)transform).rect;
        return new Vector2(
            Mathf.Lerp(
                rect.xMin,
                rect.xMax,
                normalizedPosition.x
            ),
            Mathf.Lerp(
                rect.yMin,
                rect.yMax,
                normalizedPosition.y
            )
        );
    }

    private void HighlightPoint(int index, float alpha)
    {
        if (index < 0 || index >= points.Count)
            return;

        Color color = ZoneColor(index);
        color.a = alpha;
        points[index].color = color;
    }

    private void PulsePoint(int index)
    {
        if (index < 0 || index >= points.Count)
            return;

        float pulse =
            1f +
            Mathf.Sin(Time.unscaledTime * 8f) * 0.07f;
        points[index].rectTransform.localScale =
            Vector3.one * pulse;
    }

    private void ResetPointVisuals()
    {
        for (int index = 0; index < points.Count; index++)
        {
            points[index].color = RestingColor(index);
            points[index].rectTransform.localScale =
                Vector3.one;
        }
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

    private Color RestingColor(int index)
    {
        Color color = ZoneColor(index);
        color.a = 0.24f;
        return color;
    }

    private Color ZoneColor(int index)
    {
        if (index is >= 0 and <= 2)
            return attackColor;
        if (index is >= 3 and <= 5)
            return defenseColor;
        if (index is >= 6 and <= 8)
            return movementColor;
        return Color.white;
    }
}
