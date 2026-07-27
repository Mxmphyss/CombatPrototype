using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Rayon de detection local autour de chaque point.")]
    [SerializeField]
    [Min(1f)]
    private float pointDetectionRadius = 24f;

    private readonly List<int> gesture = new();
    private readonly List<Image> points = new();
    private readonly List<Image> segments = new();

    private FighterCombat fighter;
    private CombatHUD hud;
    private int activePointerId = int.MinValue;
    private float pointerDownTime;
    private bool heldGuardStarted;
    private bool chargeStarted;
    private bool inputEnabled = true;

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
        BuildPoints();
    }

    private void Update()
    {
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
        pointerDownTime = Time.unscaledTime;
        heldGuardStarted = false;
        chargeStarted = false;
        ClearGesture();
        TryAddPoint(
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

        TryAddPoint(
            eventData.position,
            eventData.pressEventCamera
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

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

    private void TryAddPoint(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        int closestPoint =
            FindClosestPoint(screenPosition, eventCamera);
        if (closestPoint < 0 ||
            gesture.Contains(closestPoint))
        {
            return;
        }

        if (gesture.Count > 0)
            AddSegment(gesture[^1], closestPoint);

        gesture.Add(closestPoint);
        HighlightPoint(closestPoint, 1f);
        ShowFeedback(
            FormatGesture(),
            RowColor(gesture[0]),
            0.7f
        );
    }

    private int FindClosestPoint(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        RectTransform gridRect = (RectTransform)transform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                screenPosition,
                eventCamera,
                out Vector2 localPosition))
        {
            return -1;
        }

        int closest = -1;
        float closestDistance = pointDetectionRadius;

        for (int index = 0; index < points.Count; index++)
        {
            Vector2 pointPosition = gridRect.InverseTransformPoint(
                points[index].rectTransform.position
            );
            float distance = Vector2.Distance(
                localPosition,
                pointPosition
            );
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = index;
        }

        return closest;
    }

    private void AddSegment(int fromIndex, int toIndex)
    {
        Vector2 from =
            points[fromIndex].rectTransform.anchoredPosition;
        Vector2 to =
            points[toIndex].rectTransform.anchoredPosition;

        GameObject segmentObject = new("Gesture Segment");
        segmentObject.transform.SetParent(transform, false);
        segmentObject.transform.SetAsFirstSibling();

        RectTransform segmentRect =
            segmentObject.AddComponent<RectTransform>();
        segmentRect.anchorMin = segmentRect.anchorMax =
            new Vector2(0.5f, 0.5f);
        segmentRect.anchoredPosition = (from + to) * 0.5f;
        segmentRect.sizeDelta = new Vector2(
            Vector2.Distance(from, to),
            8f
        );
        segmentRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(
                to.y - from.y,
                to.x - from.x
            ) * Mathf.Rad2Deg
        );

        Image segment = segmentObject.AddComponent<Image>();
        Color segmentColor = RowColor(gesture[0]);
        segmentColor.a = 0.82f;
        segment.color = segmentColor;
        segment.raycastTarget = false;
        segments.Add(segment);
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
        heldGuardStarted = false;
        chargeStarted = false;
        ClearGesture();
    }

    private void ClearGesture()
    {
        gesture.Clear();

        foreach (Image segment in segments)
        {
            if (segment != null)
                Destroy(segment.gameObject);
        }
        segments.Clear();

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
