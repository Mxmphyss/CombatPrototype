using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatFrameDebugDisplay : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.018f, 0.025f, 0.045f, 0.96f);
    private static readonly Color ActiveColor =
        new(0.18f, 0.55f, 0.32f, 0.96f);
    private static readonly Color InactiveColor =
        new(0.22f, 0.3f, 0.42f, 0.96f);

    private readonly StringBuilder builder = new(512);

    private CombatFrameSystem frameSystem;
    private CombatSpatialController spatial;
    private FighterStats playerStats;
    private FighterStats enemyStats;
    private GameObject telemetryPanel;
    private Button toggleButton;
    private Image toggleImage;
    private Text toggleLabel;
    private Text playerTelemetry;
    private Text enemyTelemetry;
    private Text feedbackLabel;
    private float feedbackEndsAt;
    private bool visible;

    public void Initialize(
        CombatFrameSystem deterministicSystem,
        CombatSpatialController spatialAuthority,
        FighterStats playerFighterStats,
        FighterStats enemyFighterStats)
    {
        frameSystem = deterministicSystem;
        spatial = spatialAuthority;
        playerStats = playerFighterStats;
        enemyStats = enemyFighterStats;
        BuildToggle();
        BuildTelemetryPanel();
        BuildFeedback();
        SetVisible(false);

        if (frameSystem != null)
            frameSystem.OnOutcome += HandleOutcome;
    }

    private void OnDestroy()
    {
        if (frameSystem != null)
            frameSystem.OnOutcome -= HandleOutcome;
    }

    private void Update()
    {
        if (visible)
            RefreshTelemetry();

        if (feedbackLabel != null &&
            feedbackLabel.gameObject.activeSelf &&
            Time.unscaledTime >= feedbackEndsAt)
        {
            feedbackLabel.gameObject.SetActive(false);
        }
    }

    public void ResetForReplay()
    {
        feedbackEndsAt = 0f;
        if (feedbackLabel != null)
            feedbackLabel.gameObject.SetActive(false);
        if (visible)
            RefreshTelemetry();
    }

    private void BuildToggle()
    {
        GameObject buttonObject =
            new("Prototype Frame Data Toggle");
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect =
            buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-190f, -222f);
        rect.sizeDelta = new Vector2(160f, 44f);

        toggleImage = buttonObject.AddComponent<Image>();
        toggleButton = buttonObject.AddComponent<Button>();
        toggleButton.onClick.AddListener(ToggleVisible);

        toggleLabel = CreateText(
            buttonObject.transform,
            "Label",
            Vector2.zero,
            Vector2.zero,
            14,
            TextAnchor.MiddleCenter
        );
        RectTransform labelRect =
            (RectTransform)toggleLabel.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void BuildTelemetryPanel()
    {
        RectTransform hostRect =
            transform as RectTransform;
        Transform layoutParent = transform.parent;
        telemetryPanel = new GameObject(
            "Combat Frame Data Panel"
        );
        telemetryPanel.transform.SetParent(layoutParent, false);

        RectTransform rect =
            telemetryPanel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        float hostY = hostRect != null
            ? hostRect.anchoredPosition.y
            : -600f;
        float hostHeight = hostRect != null
            ? hostRect.rect.height
            : 380f;
        rect.anchoredPosition =
            new Vector2(0f, hostY - hostHeight - 12f);
        rect.sizeDelta = new Vector2(960f, 420f);

        Image background =
            telemetryPanel.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false;
        Outline outline =
            telemetryPanel.AddComponent<Outline>();
        outline.effectColor =
            new Color(0.35f, 0.72f, 1f, 0.42f);
        outline.effectDistance = new Vector2(2f, -2f);

        Text title = CreateText(
            telemetryPanel.transform,
            "Frame Data Title",
            new Vector2(20f, -12f),
            new Vector2(920f, 34f),
            21,
            TextAnchor.MiddleCenter
        );
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.62f, 0.84f, 1f, 1f);
        title.text = "FRAME DATA · 60 TICKS / SECONDE";

        playerTelemetry = CreateText(
            telemetryPanel.transform,
            "Player Frame Data",
            new Vector2(20f, -54f),
            new Vector2(450f, 350f),
            16,
            TextAnchor.UpperLeft
        );
        enemyTelemetry = CreateText(
            telemetryPanel.transform,
            "Enemy Frame Data",
            new Vector2(490f, -54f),
            new Vector2(450f, 350f),
            16,
            TextAnchor.UpperLeft
        );
    }

    private void BuildFeedback()
    {
        Transform layoutParent = transform.parent;
        feedbackLabel = CreateText(
            layoutParent,
            "Combat Frame Result Feedback",
            Vector2.zero,
            new Vector2(920f, 90f),
            42,
            TextAnchor.MiddleCenter
        );
        RectTransform rect =
            (RectTransform)feedbackLabel.transform;
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0.58f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        feedbackLabel.fontStyle = FontStyle.Bold;
        feedbackLabel.color =
            new Color(1f, 0.82f, 0.28f, 1f);
        feedbackLabel.raycastTarget = false;
        feedbackLabel.gameObject.SetActive(false);
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(parent, false);
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
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.9f, 0.94f, 1f, 1f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void ToggleVisible()
    {
        SetVisible(!visible);
    }

    private void SetVisible(bool value)
    {
        visible = value && frameSystem != null;
        if (telemetryPanel != null)
            telemetryPanel.SetActive(visible);
        if (toggleButton != null)
            toggleButton.interactable = frameSystem != null;
        if (toggleImage != null)
        {
            toggleImage.color = visible
                ? ActiveColor
                : InactiveColor;
        }
        if (toggleLabel != null)
        {
            toggleLabel.text = frameSystem == null
                ? "FRAME DATA · INDISPONIBLE"
                : visible
                    ? "MASQUER FRAME DATA"
                    : "AFFICHER FRAME DATA";
        }
        if (visible)
            RefreshTelemetry();
    }

    private void RefreshTelemetry()
    {
        if (frameSystem == null)
            return;

        CombatSpatialSnapshot snapshot =
            spatial != null ? spatial.Snapshot : default;
        WriteFighterTelemetry(
            playerTelemetry,
            "JOUEUR",
            frameSystem.PlayerRunner,
            playerStats,
            snapshot
        );
        WriteFighterTelemetry(
            enemyTelemetry,
            "ADVERSAIRE",
            frameSystem.EnemyRunner,
            enemyStats,
            snapshot
        );
    }

    private void WriteFighterTelemetry(
        Text destination,
        string label,
        CombatActionRunner runner,
        FighterStats stats,
        CombatSpatialSnapshot snapshot)
    {
        if (destination == null)
            return;
        if (runner == null)
        {
            destination.text = $"{label}\nIndisponible";
            return;
        }

        CombatFrameTelemetry telemetry =
            runner.CreateTelemetry();
        builder.Clear();
        builder.AppendLine(label);
        builder.Append("Global / local : ")
            .Append(telemetry.GlobalFrame)
            .Append(" / ")
            .AppendLine(telemetry.LocalActionFrame.ToString());
        builder.Append("Action : ")
            .Append(telemetry.CurrentAction)
            .Append(" · ")
            .AppendLine(telemetry.CurrentPhase.ToString());
        builder.Append("Startup / active / recovery : ")
            .Append(telemetry.StartupRemaining)
            .Append(" / ")
            .Append(telemetry.ActiveRemaining)
            .Append(" / ")
            .AppendLine(telemetry.RecoveryRemaining.ToString());
        builder.Append("Stop / hitstun / blockstun : ")
            .Append(telemetry.HitstopRemaining)
            .Append(" / ")
            .Append(telemetry.HitstunRemaining)
            .Append(" / ")
            .AppendLine(telemetry.BlockstunRemaining.ToString());
        builder.Append("Garde brisée / riposte : ")
            .Append(runner.GuardBreakRemaining)
            .Append(" / ")
            .AppendLine(runner.RiposteRemaining.ToString());
        builder.Append("Invuln. / parfaite / interruptible : ")
            .Append(YesNo(telemetry.Invulnerable))
            .Append(" / ")
            .Append(YesNo(telemetry.PerfectDodgeWindow))
            .Append(" / ")
            .AppendLine(YesNo(telemetry.Interruptible));
        builder.Append("Distance / orientation : ")
            .Append(snapshot.IsInitialized
                ? snapshot.Distance
                : DistanceLevel.MidRange)
            .Append(" / ")
            .AppendLine(snapshot.IsInitialized
                ? snapshot.Orientation.ToString()
                : "—");
        builder.Append("Flanc / auto-face : ")
            .Append(spatial != null
                ? spatial.FlankElapsedFrames
                : 0)
            .Append(" / ")
            .AppendLine(spatial != null &&
                        spatial.PendingAutoFace
                ? "OUI"
                : "NON");
        builder.Append("Buffer : ")
            .Append(telemetry.BufferedCommand)
            .Append(" (")
            .Append(telemetry.BufferRemainingFrames)
            .AppendLine(")");
        builder.Append("Endurance infinie : ")
            .AppendLine(stats != null &&
                        stats.HasInfiniteStamina
                ? "OUI"
                : "NON");
        builder.Append("Résultat : ")
            .AppendLine(telemetry.LastOutcome.ToString());
        builder.Append("Esquive : ")
            .Append(DodgeDestinationText(runner, telemetry));
        destination.text = builder.ToString();
    }

    private string DodgeDestinationText(
        CombatActionRunner runner,
        CombatFrameTelemetry telemetry)
    {
        if (!runner.IsDodging &&
            !telemetry.DestinationValidated &&
            !telemetry.DodgeInterrupted)
        {
            return "—";
        }

        string state = telemetry.DodgeInterrupted
            ? "interrompue"
            : telemetry.DestinationValidated
                ? "validée"
                : "prévue";
        if (spatial == null ||
            !spatial.PendingDodge.IsValid ||
            spatial.PendingDodge.Fighter != runner.Owner)
        {
            return state;
        }

        SpatialDodgeTransaction pending =
            spatial.PendingDodge;
        return $"{state} · {pending.DistanceAfter} · " +
               pending.OrientationAfter;
    }

    private void HandleOutcome(
        CombatActionRunner runner,
        CombatFrameOutcome outcome)
    {
        string result = OutcomeLabel(outcome);
        if (string.IsNullOrEmpty(result) ||
            feedbackLabel == null)
        {
            return;
        }

        string fighter = runner ==
                         frameSystem.PlayerRunner
            ? "JOUEUR"
            : "ADVERSAIRE";
        feedbackLabel.text = $"{fighter} · {result}";
        feedbackLabel.gameObject.SetActive(true);
        feedbackEndsAt = Time.unscaledTime + 0.8f;
    }

    private static string OutcomeLabel(
        CombatFrameOutcome outcome)
    {
        return outcome switch
        {
            CombatFrameOutcome.Hit => "HIT",
            CombatFrameOutcome.CounterHit => "COUNTER HIT",
            CombatFrameOutcome.Punish => "PUNISH",
            CombatFrameOutcome.Block => "BLOCK",
            CombatFrameOutcome.GuardBreak => "GUARD BREAK",
            CombatFrameOutcome.Parry => "PARRY",
            CombatFrameOutcome.Dodge => "DODGE",
            CombatFrameOutcome.PerfectDodge =>
                "PERFECT DODGE",
            CombatFrameOutcome.Trade => "TRADE",
            CombatFrameOutcome.Whiff => "WHIFF",
            CombatFrameOutcome.InterruptedDodge =>
                "INTERRUPTED",
            CombatFrameOutcome.Buffered => "BUFFERED",
            CombatFrameOutcome.Replaced => "REPLACED",
            CombatFrameOutcome.Expired => "EXPIRED",
            CombatFrameOutcome.Rejected => "REJECTED",
            _ => string.Empty
        };
    }

    private static string YesNo(bool value)
    {
        return value ? "OUI" : "NON";
    }
}
