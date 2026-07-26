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
    private const float HoldThreshold = 0.28f;

    private static readonly Color AttackColor =
        new(0.95f, 0.28f, 0.22f, 1f);
    private static readonly Color DefenseColor =
        new(0.20f, 0.65f, 0.95f, 1f);
    private static readonly Color MovementColor =
        new(0.95f, 0.82f, 0.25f, 1f);

    private readonly List<int> gesture = new();
    private readonly List<Image> points = new();
    private readonly List<Image> segments = new();

    private FighterCombat fighter;
    private int activePointerId = int.MinValue;
    private float pointerDownTime;
    private bool heldGuardStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlayer()
    {
        FighterCombat[] fighters =
            FindObjectsByType<FighterCombat>(FindObjectsSortMode.None);

        foreach (FighterCombat candidate in fighters)
        {
            if (!candidate.IsPlayerControlled)
                continue;

            CreateInterface(candidate);
            return;
        }
    }

    private static void CreateInterface(FighterCombat player)
    {
        EnsureEventSystem();

        GameObject canvasObject = new("Combat Gesture Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject gridObject = new("Combat Gesture Grid");
        gridObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = gridObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(760f, 620f);

        Image surface = gridObject.AddComponent<Image>();
        surface.color = new Color(0.02f, 0.03f, 0.06f, 0.12f);
        surface.raycastTarget = true;

        CombatGestureGrid grid =
            gridObject.AddComponent<CombatGestureGrid>();
        grid.Initialize(player);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<
            UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void Initialize(FighterCombat player)
    {
        fighter = player;
        BuildPoints();
    }

    private void Update()
    {
        if (activePointerId == int.MinValue ||
            heldGuardStarted ||
            gesture.Count != 1 ||
            gesture[0] != MiddleDefensePoint)
        {
            return;
        }

        if (Time.unscaledTime - pointerDownTime < HoldThreshold)
            return;

        heldGuardStarted = fighter.StartHeldGuard();
        if (heldGuardStarted)
            HighlightPoint(MiddleDefensePoint, 1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        pointerDownTime = Time.unscaledTime;
        heldGuardStarted = false;
        ClearGesture();
        TryAddPoint(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId ||
            heldGuardStarted)
        {
            return;
        }

        TryAddPoint(eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        if (heldGuardStarted)
            fighter.StopHeldGuard();
        else
            ExecuteGesture();

        activePointerId = int.MinValue;
        heldGuardStarted = false;
        ClearGesture();
    }

    private void BuildPoints()
    {
        const float horizontalSpacing = 250f;
        const float verticalSpacing = 190f;
        const float pointSize = 76f;

        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                int index = row * GridSize + column;
                GameObject pointObject = new($"Gesture Point {index}");
                pointObject.transform.SetParent(transform, false);

                RectTransform pointRect =
                    pointObject.AddComponent<RectTransform>();
                pointRect.anchorMin = pointRect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                pointRect.sizeDelta = new Vector2(pointSize, pointSize);
                pointRect.anchoredPosition = new Vector2(
                    (column - 1) * horizontalSpacing,
                    (1 - row) * verticalSpacing
                );

                Image point = pointObject.AddComponent<Image>();
                point.color = RestingColor(index);
                point.raycastTarget = false;
                points.Add(point);
            }
        }
    }

    private void TryAddPoint(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        int closestPoint = FindClosestPoint(screenPosition, eventCamera);
        if (closestPoint < 0 || gesture.Contains(closestPoint))
            return;

        if (gesture.Count > 0)
            AddSegment(gesture[^1], closestPoint);

        gesture.Add(closestPoint);
        HighlightPoint(closestPoint, 1f);
    }

    private int FindClosestPoint(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        int closest = -1;
        float closestDistance = 95f;

        for (int index = 0; index < points.Count; index++)
        {
            Vector2 pointPosition = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                points[index].rectTransform.position
            );
            float distance = Vector2.Distance(screenPosition, pointPosition);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = index;
        }

        return closest;
    }

    private void AddSegment(int fromIndex, int toIndex)
    {
        Vector2 from = points[fromIndex].rectTransform.anchoredPosition;
        Vector2 to = points[toIndex].rectTransform.anchoredPosition;

        GameObject segmentObject = new("Gesture Segment");
        segmentObject.transform.SetParent(transform, false);
        segmentObject.transform.SetAsFirstSibling();

        RectTransform segmentRect =
            segmentObject.AddComponent<RectTransform>();
        segmentRect.anchorMin = segmentRect.anchorMax =
            new Vector2(0.5f, 0.5f);
        segmentRect.anchoredPosition = (from + to) * 0.5f;
        segmentRect.sizeDelta =
            new Vector2(Vector2.Distance(from, to), 18f);
        segmentRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(to.y - from.y, to.x - from.x) *
            Mathf.Rad2Deg
        );

        Image segment = segmentObject.AddComponent<Image>();
        segment.color = new Color(1f, 1f, 1f, 0.9f);
        segment.raycastTarget = false;
        segments.Add(segment);
    }

    private void ExecuteGesture()
    {
        if (gesture.Count == 1)
        {
            ExecuteTap(gesture[0]);
            return;
        }

        if (Matches(6, 7, 8))
            fighter.DodgeRight();
        else if (Matches(8, 7, 6))
            fighter.DodgeLeft();
    }

    private void ExecuteTap(int point)
    {
        if (point is >= 0 and <= 2)
            fighter.LightAttack();
        else if (point is >= 3 and <= 5)
            fighter.StartDefense();
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

    private void ClearGesture()
    {
        gesture.Clear();

        foreach (Image segment in segments)
            Destroy(segment.gameObject);
        segments.Clear();

        for (int index = 0; index < points.Count; index++)
            points[index].color = RestingColor(index);
    }

    private void HighlightPoint(int index, float alpha)
    {
        Color color = RowColor(index);
        color.a = alpha;
        points[index].color = color;
    }

    private static Color RestingColor(int index)
    {
        Color color = RowColor(index);
        color.a = 0.28f;
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
