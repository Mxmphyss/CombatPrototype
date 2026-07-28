using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class GestureDebugDisplay : MonoBehaviour
{
    private const int HistoryCapacity = 10;

    [Header("Prototype - historique")]
    [Range(5, HistoryCapacity)]
    [SerializeField]
    private int maximumHistoryLines = 8;

    [Min(1f)]
    [SerializeField]
    private float displayDuration = 20f;

    [Range(14, 34)]
    [SerializeField]
    private int textSize = 22;

    private readonly string[] historyLines =
        new string[HistoryCapacity];
    private readonly float[] historyExpiryTimes =
        new float[HistoryCapacity];
    private readonly StringBuilder builder = new(256);

    private CombatGestureGrid gestureGrid;
    private Text liveText;
    private Text lastGestureText;
    private Text historyText;
    private int historyCount;
    private float currentLineExpiry;

    public void Initialize(CombatGestureGrid grid)
    {
        gestureGrid = grid;
        BuildDisplay();
        Subscribe();
        Clear();
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool historyChanged = false;

        while (historyCount > 0 &&
               now >= historyExpiryTimes[historyCount - 1])
        {
            historyCount--;
            historyLines[historyCount] = string.Empty;
            historyExpiryTimes[historyCount] = 0f;
            historyChanged = true;
        }

        if (historyChanged)
            RefreshHistory();

        if (currentLineExpiry > 0f &&
            now >= currentLineExpiry)
        {
            currentLineExpiry = 0f;
            if (liveText != null)
                liveText.text = "En cours : —";
            if (lastGestureText != null)
                lastGestureText.text = "Dernier geste : —";
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        historyCount = Mathf.Min(
            historyCount,
            Mathf.Clamp(
                maximumHistoryLines,
                5,
                HistoryCapacity
            )
        );
        ApplyTextSize(liveText);
        ApplyTextSize(lastGestureText);
        ApplyTextSize(historyText);
        RefreshHistory();
    }

    public void Clear()
    {
        historyCount = 0;
        currentLineExpiry = 0f;

        for (int index = 0;
             index < HistoryCapacity;
             index++)
        {
            historyLines[index] = string.Empty;
            historyExpiryTimes[index] = 0f;
        }

        if (liveText != null)
            liveText.text = "En cours : —";
        if (lastGestureText != null)
            lastGestureText.text = "Dernier geste : —";

        RefreshHistory();
    }

    private void Subscribe()
    {
        if (gestureGrid == null)
            return;

        gestureGrid.GestureStarted += HandleGestureStarted;
        gestureGrid.GestureUpdated += HandleGestureUpdated;
        gestureGrid.GestureCompleted += HandleGestureCompleted;
        gestureGrid.GestureFailed += HandleGestureFailed;
    }

    private void Unsubscribe()
    {
        if (gestureGrid == null)
            return;

        gestureGrid.GestureStarted -= HandleGestureStarted;
        gestureGrid.GestureUpdated -= HandleGestureUpdated;
        gestureGrid.GestureCompleted -= HandleGestureCompleted;
        gestureGrid.GestureFailed -= HandleGestureFailed;
    }

    private void HandleGestureStarted(
        GestureDebugEventData eventData)
    {
        SetLiveSequence(eventData, true);
    }

    private void HandleGestureUpdated(
        GestureDebugEventData eventData)
    {
        SetLiveSequence(eventData, true);
    }

    private void HandleGestureCompleted(
        GestureDebugEventData eventData)
    {
        string line = FormatFinalLine(eventData, true);
        CompleteLine(line);
    }

    private void HandleGestureFailed(
        GestureDebugEventData eventData)
    {
        string line = FormatFinalLine(eventData, false);
        CompleteLine(line);
    }

    private void SetLiveSequence(
        GestureDebugEventData eventData,
        bool appendUnknown)
    {
        builder.Clear();
        builder.Append("En cours : ");
        AppendZones(builder, eventData.Zones);

        if (appendUnknown)
            builder.Append(" → ?");

        liveText.text = builder.ToString();
        currentLineExpiry = 0f;
    }

    private string FormatFinalLine(
        GestureDebugEventData eventData,
        bool recognized)
    {
        builder.Clear();
        AppendZones(builder, eventData.Zones);

        if (eventData.InputKind == GestureInputKind.Hold)
            builder.Append(" (maintien)");

        builder.Append(" — ");

        if (!recognized)
        {
            builder.Append(
                eventData.RecognitionStatus ==
                GestureRecognitionStatus.Ambiguous
                    ? "Geste ambigu"
                    : "Geste invalide"
            );
            return builder.ToString();
        }

        if (!eventData.IsActionMapped)
        {
            builder.Append(
                ResolveCommandName(
                    eventData,
                    "Non assigné"
                )
            );
            return builder.ToString();
        }

        builder.Append(
            ResolveCommandName(eventData, "Commande")
        );

        if (!eventData.HasCombatResult)
        {
            builder.Append(" — Reconnue");
            return builder.ToString();
        }

        builder.Append(" — ");
        if (eventData.CombatResult ==
            CombatActionResult.Started)
        {
            builder.Append("Exécutée");
        }
        else
        {
            builder.Append("Refusée : ");
            builder.Append(
                CombatRefusalReason(
                    eventData.CombatResult
                )
            );
        }

        return builder.ToString();
    }

    private void CompleteLine(string line)
    {
        liveText.text = "En cours : —";
        lastGestureText.text = $"Dernier geste : {line}";
        currentLineExpiry =
            Time.unscaledTime + Mathf.Max(1f, displayDuration);

        int limit = Mathf.Clamp(
            maximumHistoryLines,
            5,
            HistoryCapacity
        );
        int copyCount = Mathf.Min(historyCount, limit - 1);

        for (int index = copyCount;
             index > 0;
             index--)
        {
            historyLines[index] =
                historyLines[index - 1];
            historyExpiryTimes[index] =
                historyExpiryTimes[index - 1];
        }

        historyLines[0] = line;
        historyExpiryTimes[0] = currentLineExpiry;
        historyCount = Mathf.Min(historyCount + 1, limit);
        RefreshHistory();
    }

    private void RefreshHistory()
    {
        if (historyText == null)
            return;

        builder.Clear();
        builder.Append("Historique");

        if (historyCount == 0)
        {
            builder.Append("\n—");
        }
        else
        {
            for (int index = 0;
                 index < historyCount;
                 index++)
            {
                builder.Append('\n');
                builder.Append(historyLines[index]);
            }
        }

        historyText.text = builder.ToString();
    }

    private void BuildDisplay()
    {
        liveText = CreateText(
            "Gesture Live",
            new Vector2(24f, -66f),
            new Vector2(590f, 38f),
            FontStyle.Bold
        );
        lastGestureText = CreateText(
            "Gesture Last",
            new Vector2(24f, -108f),
            new Vector2(900f, 38f),
            FontStyle.Bold
        );
        historyText = CreateText(
            "Gesture History",
            new Vector2(24f, -150f),
            new Vector2(900f, 118f),
            FontStyle.Normal
        );
        historyText.verticalOverflow =
            VerticalWrapMode.Overflow;
    }

    private Text CreateText(
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        FontStyle style)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(transform, false);

        RectTransform rect =
            textObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
        text.fontSize = textSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.UpperLeft;
        text.color = new Color(0.92f, 0.95f, 1f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private void ApplyTextSize(Text text)
    {
        if (text != null)
            text.fontSize = textSize;
    }

    private static void AppendZones(
        StringBuilder destination,
        System.Collections.Generic.IReadOnlyList<int> zones)
    {
        if (zones == null || zones.Count == 0)
        {
            destination.Append('—');
            return;
        }

        for (int index = 0; index < zones.Count; index++)
        {
            if (index > 0)
                destination.Append(" → ");

            int zone = zones[index];
            destination.Append(
                zone is >= 0 and <= 8
                    ? (char)('A' + zone)
                    : '?'
            );
        }
    }

    private static string ResolveCommandName(
        GestureDebugEventData eventData,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(eventData.CommandName))
            return eventData.CommandName;

        return eventData.GestureId != CombatGestureId.None
            ? eventData.GestureId.ToString()
            : fallback;
    }

    private static string CombatRefusalReason(
        CombatActionResult result)
    {
        return result switch
        {
            CombatActionResult.Busy => "action en cours",
            CombatActionResult.NotEnoughStamina =>
                "endurance insuffisante",
            CombatActionResult.Unavailable =>
                "combat indisponible",
            _ => "raison inconnue"
        };
    }
}
