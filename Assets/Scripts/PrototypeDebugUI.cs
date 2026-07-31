using UnityEngine;
using UnityEngine.UI;

public sealed class PrototypeDebugUI : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.025f, 0.035f, 0.055f, 0.88f);
    private static readonly Color EnabledColor =
        new(0.18f, 0.55f, 0.32f, 0.96f);
    private static readonly Color PausedColor =
        new(0.72f, 0.35f, 0.14f, 0.96f);
    private static readonly Color TraceReadyColor =
        new(0.78f, 0.16f, 0.18f, 0.98f);

    private EnemyAutoCombat enemyAI;
    private FighterCombat playerCombat;
    private FighterStats playerStats;
    private CombatSpatialController spatialController;
    private CombatCameraController cameraController;
    private CombatDistanceDebugVisualizer distanceVisualizer;
    private GestureDebugDisplay gestureDisplay;
    private Button aiToggleButton;
    private Image aiToggleImage;
    private Text aiToggleLabel;
    private Button staminaToggleButton;
    private Image staminaToggleImage;
    private Text staminaToggleLabel;
    private Text spatialStateLabel;
    private Text cameraResetLabel;
    private Text distanceToggleLabel;
    private Text dodgeTimingLabel;
    private Text cameraStateLabel;
    private CombatFrameDebugDisplay frameDebugDisplay;
    private CombatTraceRecorder traceRecorder;
    private Button traceCaptureButton;
    private Image traceCaptureImage;
    private Text traceCaptureLabel;

    public static PrototypeDebugUI Create(
        Transform parent,
        EnemyAutoCombat enemyAutoCombat,
        FighterStats playerFighterStats,
        CombatGestureGrid gestureGrid,
        CombatSpatialController spatialAuthority = null,
        CombatCameraController cameraAuthority = null,
        CombatDistanceDebugVisualizer distanceDebug = null,
        CombatFrameSystem deterministicFrameSystem = null,
        FighterStats enemyFighterStats = null,
        RectTransform enemyPanel = null,
        CombatTraceRecorder flightRecorder = null)
    {
        GameObject panelObject =
            new("Prototype Combat Debug UI");
        Transform layoutParent = enemyPanel != null
            ? enemyPanel.parent
            : parent;
        panelObject.transform.SetParent(layoutParent, false);

        RectTransform rect =
            panelObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        float enemyPanelBottom = enemyPanel != null
            ? enemyPanel.anchoredPosition.y - enemyPanel.rect.height
            : -216f;
        rect.anchoredPosition = new Vector2(
            0f,
            enemyPanelBottom - 18f
        );
        rect.sizeDelta = new Vector2(960f, 380f);

        Image background =
            panelObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false;

        Outline outline =
            panelObject.AddComponent<Outline>();
        outline.effectColor =
            new Color(0.55f, 0.72f, 0.9f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);

        PrototypeDebugUI debugUI =
            panelObject.AddComponent<PrototypeDebugUI>();
        debugUI.Initialize(
            enemyAutoCombat,
            playerFighterStats,
            gestureGrid,
            spatialAuthority,
            cameraAuthority,
            distanceDebug,
            deterministicFrameSystem,
            enemyFighterStats,
            flightRecorder
        );
        return debugUI;
    }

    private void Initialize(
        EnemyAutoCombat enemyAutoCombat,
        FighterStats playerFighterStats,
        CombatGestureGrid gestureGrid,
        CombatSpatialController spatialAuthority,
        CombatCameraController cameraAuthority,
        CombatDistanceDebugVisualizer distanceDebug,
        CombatFrameSystem deterministicFrameSystem,
        FighterStats enemyFighterStats,
        CombatTraceRecorder flightRecorder)
    {
        enemyAI = enemyAutoCombat;
        playerStats = playerFighterStats;
        playerCombat = playerStats != null
            ? playerStats.GetComponent<FighterCombat>()
            : null;
        spatialController = spatialAuthority;
        cameraController = cameraAuthority;
        distanceVisualizer = distanceDebug;
        traceRecorder = flightRecorder;
        BuildTitle();
        BuildAIToggle();
        BuildCameraReset();
        BuildDistanceToggle();
        BuildStaminaToggle();
        BuildTraceCapture();
        BuildDodgeTimingState();
        BuildCameraState();
        BuildSpatialState();

        gestureDisplay =
            gameObject.AddComponent<GestureDebugDisplay>();
        gestureDisplay.Initialize(gestureGrid);
        frameDebugDisplay =
            gameObject.AddComponent<CombatFrameDebugDisplay>();
        frameDebugDisplay.Initialize(
            deterministicFrameSystem,
            spatialController,
            playerStats,
            enemyFighterStats
        );

        if (enemyAI != null)
        {
            enemyAI.OnAIEnabledChanged +=
                HandleAIEnabledChanged;
        }
        if (spatialController != null)
        {
            spatialController.OnSnapshotChanged +=
                HandleSpatialSnapshotChanged;
        }
        if (traceRecorder != null)
        {
            traceRecorder.OnCaptureStateChanged +=
                HandleTraceCaptureStateChanged;
            traceRecorder.OnReportSaved += HandleTraceSaved;
        }

        RefreshAIToggle();
        RefreshDistanceToggle();
        RefreshStaminaToggle();
        RefreshTraceCapture();
        RefreshCameraState();
        RefreshSpatialState();
    }

    private void Update()
    {
        RefreshCameraState();
        RefreshDodgeTimingState();
    }

    private void OnDestroy()
    {
        if (enemyAI != null)
        {
            enemyAI.OnAIEnabledChanged -=
                HandleAIEnabledChanged;
        }
        if (spatialController != null)
        {
            spatialController.OnSnapshotChanged -=
                HandleSpatialSnapshotChanged;
        }
        if (traceRecorder != null)
        {
            traceRecorder.OnCaptureStateChanged -=
                HandleTraceCaptureStateChanged;
            traceRecorder.OnReportSaved -= HandleTraceSaved;
        }
    }

    public void ResetForReplay()
    {
        gestureDisplay?.Clear();
        frameDebugDisplay?.ResetForReplay();
        RefreshAIToggle();
        cameraController?.ResetCameraView(true);
        RefreshDistanceToggle();
        RefreshStaminaToggle();
        RefreshTraceCapture();
        RefreshSpatialState();
    }

    private void BuildTitle()
    {
        GameObject titleObject = new("Prototype Label");
        titleObject.transform.SetParent(transform, false);

        RectTransform rect =
            titleObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -16f);
        rect.sizeDelta = new Vector2(560f, 38f);

        Text title = titleObject.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
        title.fontSize = 24;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.62f, 0.82f, 1f, 1f);
        title.text = "OUTILS PROTOTYPE · GESTURE PAD";
        title.raycastTarget = false;
    }

    private void BuildAIToggle()
    {
        GameObject buttonObject =
            new("Prototype Enemy AI Toggle");
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect =
            buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -14f);
        rect.sizeDelta = new Vector2(330f, 48f);

        aiToggleImage = buttonObject.AddComponent<Image>();
        aiToggleImage.sprite = null;

        aiToggleButton =
            buttonObject.AddComponent<Button>();
        aiToggleButton.onClick.AddListener(ToggleEnemyAI);

        GameObject labelObject = new("Label");
        labelObject.transform.SetParent(
            buttonObject.transform,
            false
        );

        RectTransform labelRect =
            labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        aiToggleLabel = labelObject.AddComponent<Text>();
        aiToggleLabel.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        aiToggleLabel.fontSize = 21;
        aiToggleLabel.fontStyle = FontStyle.Bold;
        aiToggleLabel.alignment =
            TextAnchor.MiddleCenter;
        aiToggleLabel.color = Color.white;
        aiToggleLabel.raycastTarget = false;
    }

    private void BuildSpatialState()
    {
        GameObject labelObject =
            new("Prototype Spatial State");
        labelObject.transform.SetParent(transform, false);

        RectTransform rect =
            labelObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -338f);
        rect.sizeDelta = new Vector2(910f, 30f);

        spatialStateLabel =
            labelObject.AddComponent<Text>();
        spatialStateLabel.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        spatialStateLabel.fontSize = 19;
        spatialStateLabel.fontStyle = FontStyle.Bold;
        spatialStateLabel.alignment =
            TextAnchor.MiddleLeft;
        spatialStateLabel.color =
            new Color(0.72f, 0.86f, 1f, 1f);
        spatialStateLabel.raycastTarget = false;
    }

    private void BuildCameraState()
    {
        GameObject labelObject =
            new("Prototype Camera State");
        labelObject.transform.SetParent(transform, false);
        RectTransform rect =
            labelObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -306f);
        rect.sizeDelta = new Vector2(910f, 28f);
        cameraStateLabel = labelObject.AddComponent<Text>();
        cameraStateLabel.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        cameraStateLabel.fontSize = 17;
        cameraStateLabel.fontStyle = FontStyle.Bold;
        cameraStateLabel.alignment = TextAnchor.MiddleLeft;
        cameraStateLabel.color =
            new Color(0.68f, 0.8f, 0.95f, 1f);
        cameraStateLabel.raycastTarget = false;
    }

    private void BuildDodgeTimingState()
    {
        GameObject labelObject =
            new("Prototype Dodge Timing State");
        labelObject.transform.SetParent(transform, false);
        RectTransform rect =
            labelObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -274f);
        rect.sizeDelta = new Vector2(910f, 26f);
        dodgeTimingLabel = labelObject.AddComponent<Text>();
        dodgeTimingLabel.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        dodgeTimingLabel.fontSize = 16;
        dodgeTimingLabel.fontStyle = FontStyle.Bold;
        dodgeTimingLabel.alignment =
            TextAnchor.MiddleLeft;
        dodgeTimingLabel.color =
            new Color(0.9f, 0.78f, 0.36f, 1f);
        dodgeTimingLabel.raycastTarget = false;
        RefreshDodgeTimingState();
    }

    private void RefreshDodgeTimingState()
    {
        if (dodgeTimingLabel == null)
            return;

        if (playerCombat == null)
        {
            dodgeTimingLabel.text =
                "ESQUIVE · timings indisponibles";
            return;
        }

        float vulnerableEnd =
            playerCombat.DodgeStartupDuration;
        float invulnerableEnd =
            vulnerableEnd +
            playerCombat.DodgeInvulnerabilityDuration;
        float perfectStart =
            vulnerableEnd +
            (playerCombat.DodgeInvulnerabilityDuration -
             playerCombat.PerfectDodgeWindow) * 0.5f;
        float perfectEnd =
            perfectStart +
            playerCombat.PerfectDodgeWindow;
        float recoveryEnd =
            invulnerableEnd +
            playerCombat.DodgeRecoveryDuration;

        dodgeTimingLabel.text =
            $"ESQUIVE · vuln. 0-{vulnerableEnd:0.00}s" +
            $" · invuln. {vulnerableEnd:0.00}-{invulnerableEnd:0.00}s" +
            $" · parfaite {perfectStart:0.00}-{perfectEnd:0.00}s" +
            $" · fin vuln. {invulnerableEnd:0.00}-{recoveryEnd:0.00}s" +
            $" · {DodgePhaseLabel(playerCombat.CurrentDodgeWindowPhase)}";
    }

    private void RefreshCameraState()
    {
        if (cameraStateLabel == null)
            return;

        cameraStateLabel.text = cameraController == null
            ? "CAMERA · indisponible"
            : $"CAMERA · {(cameraController.IsManualViewActive ? "MANUELLE" : "AUTO")}" +
              $" · zoom {cameraController.CurrentZoom:0.0}";
    }

    private void BuildCameraReset()
    {
        GameObject buttonObject =
            CreatePrototypeButton(
                "Prototype Camera Reset",
                new Vector2(-20f, -72f),
                new Vector2(330f, 44f),
                out cameraResetLabel
            );
        cameraResetLabel.text = "Reinitialiser la camera";
        buttonObject.GetComponent<Button>()
            .onClick.AddListener(ResetCamera);
    }

    private void BuildDistanceToggle()
    {
        GameObject buttonObject =
            CreatePrototypeButton(
                "Prototype Distance Toggle",
                new Vector2(-20f, -122f),
                new Vector2(330f, 44f),
                out distanceToggleLabel
            );
        buttonObject.GetComponent<Button>()
            .onClick.AddListener(ToggleDistanceCircles);
    }

    private void BuildStaminaToggle()
    {
        GameObject buttonObject =
            CreatePrototypeButton(
                "Prototype Infinite Stamina Toggle",
                new Vector2(-20f, -172f),
                new Vector2(330f, 44f),
                out staminaToggleLabel
            );
        staminaToggleImage =
            buttonObject.GetComponent<Image>();
        staminaToggleButton =
            buttonObject.GetComponent<Button>();
        staminaToggleButton.onClick.AddListener(
            ToggleInfiniteStamina
        );
    }

    private void BuildTraceCapture()
    {
        GameObject buttonObject =
            CreatePrototypeButton(
                "Prototype Combat Trace Capture",
                new Vector2(-20f, -222f),
                new Vector2(160f, 44f),
                out traceCaptureLabel
            );
        traceCaptureLabel.fontSize = 16;
        traceCaptureImage = buttonObject.GetComponent<Image>();
        traceCaptureButton = buttonObject.GetComponent<Button>();
        traceCaptureButton.onClick.AddListener(CaptureCombatTrace);
    }

    private GameObject CreatePrototypeButton(
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        out Text label)
    {
        GameObject buttonObject = new(objectName);
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect =
            buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.34f, 0.5f, 0.96f);
        buttonObject.AddComponent<Button>();

        GameObject labelObject = new("Label");
        labelObject.transform.SetParent(
            buttonObject.transform,
            false
        );
        RectTransform labelRect =
            labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        return buttonObject;
    }

    private void ResetCamera()
    {
        cameraController?.ResetCameraView(true);
        RefreshSpatialState();
    }

    private void ToggleDistanceCircles()
    {
        distanceVisualizer?.ToggleVisible();
        RefreshDistanceToggle();
    }

    private void ToggleInfiniteStamina()
    {
        if (playerStats == null)
            return;

        playerStats.SetInfiniteStamina(
            !playerStats.HasInfiniteStamina
        );
        RefreshStaminaToggle();
    }

    private void CaptureCombatTrace()
    {
        if (traceRecorder == null)
            return;

        traceRecorder.CaptureReport();
        RefreshTraceCapture();
    }

    private void HandleTraceCaptureStateChanged(bool capturing)
    {
        RefreshTraceCapture();
    }

    private void HandleTraceSaved(string path)
    {
        RefreshTraceCapture();
    }

    private void RefreshTraceCapture()
    {
        bool available = traceRecorder != null;
        bool capturing =
            available && traceRecorder.CapturePending;
        bool saved =
            available &&
            !string.IsNullOrEmpty(
                traceRecorder.LastSavedTracePath
            );

        if (traceCaptureButton != null)
        {
            traceCaptureButton.interactable =
                available && !capturing;
        }
        if (traceCaptureImage != null)
        {
            traceCaptureImage.color = capturing
                ? PausedColor
                : saved
                    ? EnabledColor
                    : TraceReadyColor;
        }
        if (traceCaptureLabel != null)
        {
            traceCaptureLabel.text = !available
                ? "SIGNALER LE BUG - INDISPONIBLE"
                : capturing
                    ? "CAPTURE EN COURS..."
                    : saved
                        ? "TRACE SAUVEE - NOUVEAU RAPPORT"
                        : "SIGNALER UN BUG";
        }
    }

    private void RefreshDistanceToggle()
    {
        if (distanceToggleLabel == null)
            return;

        distanceToggleLabel.text =
            distanceVisualizer != null &&
            distanceVisualizer.IsVisible
                ? "Masquer les distances"
                : "Afficher les distances";
    }

    private void RefreshStaminaToggle()
    {
        bool available = playerStats != null;
        bool enabled =
            available && playerStats.HasInfiniteStamina;

        if (staminaToggleButton != null)
            staminaToggleButton.interactable = available;

        if (staminaToggleImage != null)
        {
            staminaToggleImage.color = enabled
                ? EnabledColor
                : PausedColor;
        }

        if (staminaToggleLabel != null)
        {
            staminaToggleLabel.text = !available
                ? "ENDURANCE ILLIMITEE · INDISPONIBLE"
                : enabled
                    ? "ENDURANCE ILLIMITEE · ACTIVE"
                    : "ENDURANCE ILLIMITEE · INACTIVE";
        }
    }

    private void ToggleEnemyAI()
    {
        if (enemyAI == null)
            return;

        enemyAI.SetAIEnabled(!enemyAI.EnemyAIEnabled);
    }

    private void HandleAIEnabledChanged(bool enabled)
    {
        RefreshAIToggle();
    }

    private void HandleSpatialSnapshotChanged(
        CombatSpatialSnapshot snapshot)
    {
        RefreshSpatialState(snapshot);
    }

    private void RefreshAIToggle()
    {
        bool available = enemyAI != null;
        bool enabled =
            available && enemyAI.EnemyAIEnabled;

        if (aiToggleButton != null)
            aiToggleButton.interactable = available;

        if (aiToggleImage != null)
        {
            aiToggleImage.color = enabled
                ? EnabledColor
                : PausedColor;
        }

        if (aiToggleLabel != null)
        {
            aiToggleLabel.text = !available
                ? "PROTO · IA INDISPONIBLE"
                : enabled
                    ? "PROTO · IA ACTIVE"
                    : "PROTO · IA EN PAUSE";
        }
    }

    private void RefreshSpatialState()
    {
        if (spatialController == null)
        {
            if (spatialStateLabel != null)
                spatialStateLabel.text =
                    "ESPACE · indisponible";
            return;
        }

        RefreshSpatialState(spatialController.Snapshot);
    }

    private void RefreshSpatialState(
        CombatSpatialSnapshot snapshot)
    {
        if (spatialStateLabel == null)
            return;

        string distanceState = DistanceLabel(snapshot.Distance);
        if (snapshot.HasPendingDodge &&
            spatialController.PendingDodge.IsValid)
        {
            SpatialDodgeTransaction pending =
                spatialController.PendingDodge;
            distanceState =
                $"{DistanceLabel(pending.DistanceBefore)}" +
                $" -> {DistanceLabel(pending.DistanceAfter)}";
        }

        spatialStateLabel.text =
            $"ESPACE · {distanceState}" +
            $" · {OrientationLabel(snapshot.Orientation)}" +
            $" · {MovementLabel(snapshot.FirstMovement)}";
    }

    private static string DistanceLabel(DistanceLevel distance)
    {
        return distance switch
        {
            DistanceLevel.CloseRange => "Proche",
            DistanceLevel.MidRange => "Moyenne",
            DistanceLevel.LongRange => "Longue",
            _ => distance.ToString()
        };
    }

    private static string OrientationLabel(
        RelativeOrientation orientation)
    {
        return orientation switch
        {
            RelativeOrientation.Face => "Face",
            RelativeOrientation.LeftFlank =>
                "Flanc gauche",
            RelativeOrientation.RightFlank =>
                "Flanc droit",
            RelativeOrientation.Back => "Dos",
            _ => orientation.ToString()
        };
    }

    private static string MovementLabel(
        SpatialMovementType movement)
    {
        return movement switch
        {
            SpatialMovementType.Advance => "Avance",
            SpatialMovementType.Retreat => "Recule",
            SpatialMovementType.StrafeLeft =>
                "Marche gauche",
            SpatialMovementType.StrafeRight =>
                "Marche droite",
            _ => "Immobile"
        };
    }

    private static string DodgePhaseLabel(
        DodgeWindowPhase phase)
    {
        return phase switch
        {
            DodgeWindowPhase.StartupVulnerable =>
                "VULNERABLE - DEBUT",
            DodgeWindowPhase.Invulnerable =>
                "INVULNERABLE",
            DodgeWindowPhase.Perfect =>
                "FENETRE PARFAITE",
            DodgeWindowPhase.RecoveryVulnerable =>
                "VULNERABLE - FIN",
            _ => "PRETE"
        };
    }
}
