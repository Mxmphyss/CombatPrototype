using UnityEngine;

public sealed class CombatManager : MonoBehaviour
{
    private FighterCombat playerCombat;
    private FighterCombat enemyCombat;
    private FighterStats playerStats;
    private FighterStats enemyStats;
    private CombatHUD hud;
    private EnemyAutoCombat enemyAI;
    private CombatFeedbackEffects feedbackEffects;
    private PrototypeDebugUI prototypeDebugUI;
    private CombatSpatialController spatialController;
    private CombatCameraController cameraController;
    private CombatDistanceDebugVisualizer distanceVisualizer;
    private CombatFrameClock frameClock;
    private CombatFrameSystem frameSystem;
    private CombatTraceRecorder traceRecorder;
    private bool combatEnded;
    private bool isResetting;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForCombatScene()
    {
        if (FindFirstObjectByType<CombatManager>() != null)
            return;

        FighterCombat[] fighters =
            FindObjectsByType<FighterCombat>(
                FindObjectsSortMode.None
            );

        FighterCombat player = null;
        FighterCombat enemy = null;

        foreach (FighterCombat fighter in fighters)
        {
            if (fighter.IsPlayerControlled)
                player = fighter;
            else if (enemy == null)
                enemy = fighter;
        }

        if (player == null || enemy == null)
            return;

        GameObject managerObject = new("Combat Manager");
        CombatManager manager =
            managerObject.AddComponent<CombatManager>();
        manager.Initialize(player, enemy);
    }

    private void Initialize(
        FighterCombat player,
        FighterCombat enemy)
    {
        playerCombat = player;
        enemyCombat = enemy;
        playerStats = player.Stats;
        enemyStats = enemy.Stats;

        CombatRulesConfig sharedRules =
            playerCombat.Rules;
        playerCombat.SetCombatRules(sharedRules);
        enemyCombat.SetCombatRules(sharedRules);

        if (playerStats == null || enemyStats == null)
        {
            Debug.LogError(
                "CombatManager requires FighterStats on both fighters."
            );
            enabled = false;
            return;
        }

        spatialController =
            gameObject.AddComponent<CombatSpatialController>();
        if (!spatialController.Initialize(
                playerCombat,
                enemyCombat
            ))
        {
            Debug.LogError(
                "CombatManager could not initialize spatial authority."
            );
            enabled = false;
            return;
        }

        spatialController.Configure(
            CreateSpatialSettings(sharedRules)
        );
        playerCombat.SetSpatialController(spatialController);
        enemyCombat.SetSpatialController(spatialController);

        frameClock = gameObject.AddComponent<CombatFrameClock>();
        frameSystem = gameObject.AddComponent<CombatFrameSystem>();
        frameSystem.Initialize(
            frameClock,
            playerCombat,
            enemyCombat,
            spatialController,
            sharedRules
        );

        hud = CombatHUD.Create(
            playerCombat,
            enemyCombat,
            RestartCombat
        );

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraController =
                mainCamera.GetComponent<CombatCameraController>();
            if (cameraController == null)
            {
                cameraController =
                    mainCamera.gameObject.AddComponent<
                        CombatCameraController>();
            }
            cameraController.Initialize(
                mainCamera,
                playerCombat,
                enemyCombat,
                spatialController
            );
            hud.GestureGrid?.SetCameraController(
                cameraController
            );
        }

        distanceVisualizer =
            gameObject.AddComponent<
                CombatDistanceDebugVisualizer>();
        distanceVisualizer.Initialize(
            spatialController,
            playerCombat,
            enemyCombat
        );

        enemyAI = enemyCombat.GetComponent<EnemyAutoCombat>();
        if (enemyAI == null)
        {
            enemyAI =
                enemyCombat.gameObject.AddComponent<
                    EnemyAutoCombat>();
        }

        traceRecorder =
            gameObject.AddComponent<CombatTraceRecorder>();
        traceRecorder.Initialize(
            playerCombat,
            enemyCombat,
            frameSystem,
            spatialController,
            hud.GestureGrid,
            enemyAI,
            mainCamera
        );

        prototypeDebugUI = PrototypeDebugUI.Create(
            hud.transform,
            enemyAI,
            playerStats,
            hud.GestureGrid,
            spatialController,
            cameraController,
            distanceVisualizer,
            frameSystem,
            enemyStats,
            hud.EnemyPanel,
            traceRecorder
        );

        feedbackEffects =
            gameObject.AddComponent<CombatFeedbackEffects>();
        feedbackEffects.Initialize(
            playerCombat,
            enemyCombat,
            mainCamera,
            spatialController,
            cameraController
        );

        Subscribe();
        StartCombat();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        CancelTransientCombatState(true);
    }

    public void RestartCombat()
    {
        if (isResetting || !combatEnded)
            return;

        isResetting = true;
        traceRecorder?.RecordSystemEvent("REPLAY_RESET_STARTED");

        CancelTransientCombatState(true);

        playerStats.ResetStats();
        enemyStats.ResetStats();

        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();
        spatialController?.ResetDuel();
        spatialController?.SetCombatEnabled(true);
        frameSystem?.ResetSystem();

        combatEnded = false;
        hud.HideEndState();
        hud.RefreshAll();
        hud.SetGridEnabled(true);

        enemyAI.Initialize(
            enemyCombat,
            playerCombat,
            hud,
            spatialController,
            frameSystem
        );
        prototypeDebugUI?.ResetForReplay();
        cameraController?.ResetCameraView(true);
        distanceVisualizer?.ResetForReplay();
        traceRecorder?.RecordSystemEvent("REPLAY_RESET_COMPLETED");
        isResetting = false;
    }

    private void StartCombat()
    {
        traceRecorder?.RecordSystemEvent("COMBAT_STARTING");
        combatEnded = false;
        hud.SetGridEnabled(false);
        hud.GestureGrid?.ResetForReplay();
        spatialController?.SetCombatEnabled(false);
        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();
        spatialController?.ResetDuel();
        spatialController?.SetCombatEnabled(true);
        frameSystem?.ResetSystem();
        hud.HideEndState();
        hud.SetGridEnabled(true);
        hud.RefreshAll();
        enemyAI.Initialize(
            enemyCombat,
            playerCombat,
            hud,
            spatialController,
            frameSystem
        );
        prototypeDebugUI?.ResetForReplay();
        cameraController?.ResetCameraView(true);
        distanceVisualizer?.ResetForReplay();
        traceRecorder?.RecordSystemEvent("COMBAT_STARTED");
    }

    private void EndCombat(bool playerWon)
    {
        if (combatEnded || isResetting)
            return;

        combatEnded = true;
        traceRecorder?.RecordSystemEvent(
            playerWon
                ? "COMBAT_ENDED_PLAYER_WIN"
                : "COMBAT_ENDED_PLAYER_LOSS"
        );
        CancelTransientCombatState(true);
        hud.ShowEndState(playerWon);
    }

    private void CancelTransientCombatState(
        bool resetFeedback)
    {
        if (enemyAI != null)
            enemyAI.StopAI();
        if (cameraController != null)
            cameraController.CancelTransientInput();
        if (hud != null)
        {
            hud.SetGridEnabled(false);
            CombatGestureGrid gestureGrid = hud.GestureGrid;
            if (gestureGrid != null)
                gestureGrid.ResetForReplay();
        }
        if (resetFeedback && feedbackEffects != null)
            feedbackEffects.ResetEffects();

        if (spatialController != null)
            spatialController.SetCombatEnabled(false);
        if (frameSystem != null)
            frameSystem.SetCombatEnabled(false);
        if (playerCombat != null)
            playerCombat.SetCombatEnabled(false);
        if (enemyCombat != null)
            enemyCombat.SetCombatEnabled(false);
    }

    private void Subscribe()
    {
        playerStats.OnDeath += HandleFighterDeath;
        enemyStats.OnDeath += HandleFighterDeath;
    }

    private void Unsubscribe()
    {
        if (playerStats != null)
            playerStats.OnDeath -= HandleFighterDeath;
        if (enemyStats != null)
            enemyStats.OnDeath -= HandleFighterDeath;
    }

    private void HandleFighterDeath(FighterStats deadFighter)
    {
        EndCombat(deadFighter == enemyStats);
    }

    private static CombatSpatialSettings CreateSpatialSettings(
        CombatRulesConfig rules)
    {
        float closeMidBoundary =
            (rules.CloseDistance + rules.MidDistance) * 0.5f;
        float midLongBoundary =
            (rules.MidDistance + rules.LongDistance) * 0.5f;
        float maximumTolerance = Mathf.Max(
            0f,
            Mathf.Min(
                rules.MidDistance - closeMidBoundary,
                midLongBoundary - rules.MidDistance
            ) - 0.01f
        );
        float safeTolerance = Mathf.Min(
            rules.DistanceTolerance,
            maximumTolerance
        );

        return new CombatSpatialSettings
        {
            MinimumDistance = rules.CloseDistance,
            CloseRangeUpperBound =
                Mathf.Max(
                    rules.CloseDistance,
                    closeMidBoundary -
                    safeTolerance
                ),
            MidRangeUpperBound =
                Mathf.Max(
                    closeMidBoundary,
                    midLongBoundary -
                    safeTolerance
                ),
            MidRangeDistance = rules.MidDistance,
            MaximumDistance = rules.LongDistance,
            AdvanceSpeed = rules.ForwardMoveSpeed,
            RetreatSpeed = rules.BackwardMoveSpeed,
            StrafeSpeed = rules.LateralMoveSpeed,
            RotationSpeed = rules.RotationSpeed,
            DodgeOrientationAngle =
                rules.DodgeOrientationAngle,
            DistanceDodgeJumpHeight =
                rules.DistanceDodgeJumpHeight,
            AutoFaceFlanks = true,
            FlankAutoFaceDelay =
                rules.FlankAutoFaceDelay,
            FaceDamageMultiplier = 1f,
            FlankDamageMultiplier =
                rules.FlankDamageMultiplier,
            BackDamageMultiplier =
                rules.BackDamageMultiplier
        };
    }
}
