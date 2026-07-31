using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class V07PlayModeValidation
{
    private const string ActiveKey =
        "CombatPrototype.V07PlayMode.Active";
    private const string ResultKey =
        "CombatPrototype.V07PlayMode.Result";
    private const string ScenePath =
        "Assets/Scenes/CombatArena.unity";
    private const float Tolerance = 0.01f;

    private static double startedAt;
    private static CombatFrameSystem frameSystem;
    private static CombatFrameClock clock;
    private static CombatSpatialController spatial;
    private static FighterCombat player;
    private static FighterCombat enemy;
    private static FighterStats playerStats;
    private static FighterStats enemyStats;
    private static EnemyAutoCombat enemyAI;
    private static CombatCameraController cameraController;
    private static CombatTraceRecorder traceRecorder;

    static V07PlayModeValidation()
    {
        EditorApplication.playModeStateChanged -=
            HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "V07 PlayMode validation requires Edit Mode."
            );
        }

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(ResultKey, 0);
        startedAt = EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            startedAt = EditorApplication.timeSinceStartup;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        int result = SessionState.GetInt(ResultKey, -1);
        SessionState.EraseBool(ActiveKey);
        SessionState.EraseInt(ResultKey);
        if (result == 1)
        {
            Debug.Log(
                "V07PlayModeValidation: deterministic attacks, " +
                "enemy telegraph, stable off-axis camera, telegraphed " +
                "dodge stability, whiff feedback, trade, buffer " +
                "clearing, guard, parry, guard break, dodges, " +
                "permutation invulnerability, flank timer, infinite " +
                "stamina, replay reset and combat flight recorder " +
                "capture passed."
            );
            EditorApplication.Exit(0);
        }
        else
        {
            EditorApplication.Exit(1);
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) ||
            !EditorApplication.isPlaying)
        {
            return;
        }

        try
        {
            if (!TryResolveRuntime())
            {
                if (EditorApplication.timeSinceStartup - startedAt > 25d)
                {
                    throw new InvalidOperationException(
                        "CombatArena deterministic runtime did not initialize."
                    );
                }
                return;
            }

            RunValidation();
            SessionState.SetInt(ResultKey, 1);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "V07PlayModeValidation failed: " + exception
            );
            SessionState.SetInt(ResultKey, -1);
            EditorApplication.ExitPlaymode();
        }
    }

    private static bool TryResolveRuntime()
    {
        frameSystem ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatFrameSystem>();
        spatial ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatSpatialController>();
        if (player == null || enemy == null)
        {
            FighterCombat[] fighters =
                UnityEngine.Object.FindObjectsByType<FighterCombat>(
                    FindObjectsSortMode.None
                );
            player = fighters.FirstOrDefault(
                fighter => fighter.IsPlayerControlled
            );
            enemy = fighters.FirstOrDefault(
                fighter => !fighter.IsPlayerControlled
            );
        }

        clock = frameSystem != null ? frameSystem.Clock : null;
        playerStats = player != null ? player.Stats : null;
        enemyStats = enemy != null ? enemy.Stats : null;
        enemyAI ??=
            UnityEngine.Object.FindFirstObjectByType<
                EnemyAutoCombat>();
        cameraController ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatCameraController>();
        traceRecorder ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatTraceRecorder>();
        enemyAI?.SetAIEnabled(false);

        return frameSystem != null &&
               clock != null &&
               spatial != null &&
               player != null &&
               enemy != null &&
               player.FrameRunner != null &&
               enemy.FrameRunner != null &&
               playerStats != null &&
               enemyStats != null &&
               cameraController != null &&
               traceRecorder != null;
    }

    private static void RunValidation()
    {
        ValidateAttackAndHitstop();
        ValidateAttackBAndC();
        ValidateEnemyAttackTelegraph();
        ValidateOffAxisEnemyAttackKeepsCameraStable();
        ValidateTelegraphedDodgeKeepsFlank();
        ValidateOffAxisWhiff();
        ValidateTrade();
        ValidateBufferExpiry();
        ValidateParry();
        ValidateHeldGuardAndGuardBreak();
        ValidateBlockClearsBuffer();
        ValidateInterruptedDodge();
        ValidateDistanceDodgeJumpArc();
        ValidateDodgeWindows();
        ValidatePermutation();
        ValidatePermutationInvulnerability();
        ValidateFlankTimer();
        ValidateInfiniteStamina();
        ValidateReplayReset();
        ValidateFlightRecorder();
    }

    private static void ValidateAttackAndHitstop()
    {
        ResetScenario();
        RequireStarted(player.LightAttack(), "Attack A");
        Advance(1);
        RequireNear(
            playerStats.CurrentStamina,
            90f,
            "Attack A stamina cost"
        );
        Advance(7);
        RequireNear(
            enemyStats.CurrentHealth,
            80f,
            "Attack A damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Hit,
            "Attack A outcome"
        );
        int frozenLocalFrame =
            player.FrameRunner.LocalActionFrame;
        Advance(3);
        RequireEqual(
            player.FrameRunner.LocalActionFrame,
            frozenLocalFrame,
            "Hitstop froze local action frame"
        );
        RequireEqual(
            player.FrameRunner.HitstopRemaining,
            0,
            "Hitstop duration"
        );
    }

    private static void ValidateOffAxisWhiff()
    {
        ResetScenario();
        CombatHitResult? feedbackResult = null;
        void CaptureFeedback(CombatImpact impact)
        {
            if (impact.Attacker == player)
                feedbackResult = impact.Result;
        }

        player.OnAttackResolved += CaptureFeedback;
        player.transform.rotation =
            Quaternion.Euler(0f, 180f, 0f);
        try
        {
            RequireStarted(player.LightAttack(), "Off-axis attack");
            Advance(40);
        }
        finally
        {
            player.OnAttackResolved -= CaptureFeedback;
        }
        RequireNear(
            enemyStats.CurrentHealth,
            100f,
            "Off-axis whiff damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Whiff,
            "Off-axis whiff outcome"
        );
        RequireEqual(
            feedbackResult,
            (CombatHitResult?)CombatHitResult.Missed,
            "Off-axis whiff feedback"
        );
    }

    private static void ValidateEnemyAttackTelegraph()
    {
        ResetScenario();
        Require(
            enemyAI != null,
            "Enemy AI was not available for telegraph validation."
        );
        enemyAI.SetAIEnabled(true);

        int searchLimit = 240;
        while (!enemyAI.IsAttackTelegraphing &&
               searchLimit-- > 0)
        {
            Advance(1);
        }

        Require(
            enemyAI.IsAttackTelegraphing,
            "Enemy AI did not announce its attack."
        );
        Require(
            CombatActionRunner.IsAttack(
                enemyAI.TelegraphedAttack
            ),
            "Enemy telegraph did not reserve an attack."
        );
        int remaining =
            enemyAI.AttackTelegraphRemainingFrames;
        RequireEqual(
            remaining,
            enemyAI.AttackTelegraphDurationFrames,
            "Enemy telegraph duration"
        );
        RequireEqual(
            enemy.FrameRunner.CurrentActionId,
            CombatActionId.None,
            "Enemy attacked before the telegraph ended"
        );

        if (remaining > 1)
            Advance(remaining - 1);
        RequireEqual(
            enemy.FrameRunner.CurrentActionId,
            CombatActionId.None,
            "Enemy attacked during the telegraph"
        );
        Advance(2);
        Require(
            CombatActionRunner.IsAttack(
                enemy.FrameRunner.CurrentActionId
            ),
            "Enemy attack did not start after the telegraph."
        );

        enemyAI.SetAIEnabled(false);
        ResetScenario();
    }

    private static void ValidateOffAxisEnemyAttackKeepsCameraStable()
    {
        ResetScenario();
        enemyAI.SetAIEnabled(false);
        RequireStarted(
            player.DodgeRight(),
            "Camera-stability flank dodge"
        );
        Advance(26);
        Require(
            spatial.CurrentOrientation is
                RelativeOrientation.LeftFlank or
                RelativeOrientation.RightFlank,
            "Camera-stability dodge did not create a flank."
        );

        cameraController.ResetCameraView(true);
        Vector3 stablePlayerPosition = player.transform.position;
        Quaternion stablePlayerRotation = player.transform.rotation;
        Vector3 stableCameraPosition =
            Camera.main.transform.position;
        Quaternion stableCameraRotation =
            Camera.main.transform.rotation;
        float stableZoom = cameraController.CurrentZoom;

        RequireStarted(
            enemy.LightAttack(),
            "Off-axis enemy camera-stability attack"
        );
        for (int frame = 0; frame < 24; frame++)
        {
            Advance(1);
            cameraController.ResetCameraView(true);
            Require(
                Vector3.Distance(
                    stablePlayerPosition,
                    player.transform.position
                ) <= Tolerance,
                "Enemy lunge moved the flanking player."
            );
            Require(
                Quaternion.Angle(
                    stablePlayerRotation,
                    player.transform.rotation
                ) <= Tolerance,
                "Enemy lunge rotated the flanking player."
            );
            Require(
                Vector3.Distance(
                    stableCameraPosition,
                    Camera.main.transform.position
                ) <= Tolerance,
                "Enemy lunge displaced the camera."
            );
            Require(
                Quaternion.Angle(
                    stableCameraRotation,
                    Camera.main.transform.rotation
                ) <= Tolerance,
                "Enemy lunge rotated the camera."
            );
            RequireNear(
                cameraController.CurrentZoom,
                stableZoom,
                "Enemy lunge camera zoom"
            );
        }

        RequireNear(
            playerStats.CurrentHealth,
            100f,
            "Off-axis enemy attack damage"
        );
        RequireEqual(
            enemy.FrameRunner.LastOutcome,
            CombatFrameOutcome.Whiff,
            "Off-axis enemy attack outcome"
        );
    }

    private static void ValidateTelegraphedDodgeKeepsFlank()
    {
        ResetScenario();
        CombatRulesConfig rules = enemy.Rules;
        float originalProbability = GetPrivateField<float>(
            rules,
            "aiCompensationProbability"
        );

        try
        {
            SetPrivateField(
                rules,
                "aiCompensationProbability",
                1f
            );
            enemyAI.SetAIEnabled(true);

            int searchLimit = 240;
            while (!enemyAI.IsAttackTelegraphing &&
                   searchLimit-- > 0)
            {
                Advance(1);
            }

            Require(
                enemyAI.IsAttackTelegraphing,
                "Enemy AI did not telegraph the dodge-stability attack."
            );
            RequireStarted(
                player.DodgeRight(),
                "Telegraphed right dodge"
            );
            Advance(20);
            Require(
                spatial.CurrentOrientation is
                    RelativeOrientation.LeftFlank or
                    RelativeOrientation.RightFlank,
                "Telegraphed dodge did not commit its flank."
            );
            RequireEqual(
                GetPrivateField<int>(
                    enemyAI,
                    "compensationDueFrame"
                ),
                -1,
                "Telegraphed dodge AI compensation"
            );

            Vector3 committedPlayerPosition =
                spatial.TryGetNeutralPosition(
                    player,
                    out Vector3 neutralPosition)
                    ? neutralPosition
                    : player.transform.position;
            Advance(60);
            Require(
                spatial.CurrentOrientation is
                    RelativeOrientation.LeftFlank or
                    RelativeOrientation.RightFlank,
                "Telegraphed dodge returned to Face prematurely."
            );
            Require(
                Vector3.Distance(
                    committedPlayerPosition,
                    player.transform.position
                ) <= Tolerance,
                "Telegraphed dodge snapped back after commit."
            );
        }
        finally
        {
            enemyAI.SetAIEnabled(false);
            SetPrivateField(
                rules,
                "aiCompensationProbability",
                originalProbability
            );
            ResetScenario();
        }
    }

    private static void ValidateAttackBAndC()
    {
        ResetScenario();
        RequireStarted(player.MediumAttack(), "Attack B");
        Advance(12);
        RequireNear(
            enemyStats.CurrentHealth,
            70f,
            "Attack B damage"
        );
        RequireNear(
            playerStats.CurrentStamina,
            82f,
            "Attack B stamina cost"
        );

        ResetScenario();
        RequireStarted(player.HeavyAttack(), "Attack C");
        Advance(19);
        RequireNear(
            enemyStats.CurrentHealth,
            55f,
            "Attack C damage"
        );
        RequireNear(
            playerStats.CurrentStamina,
            70f,
            "Attack C stamina cost"
        );
    }

    private static void ValidateTrade()
    {
        ResetScenario();
        RequireStarted(player.LightAttack(), "Player trade attack");
        RequireStarted(enemy.LightAttack(), "Enemy trade attack");
        Advance(8);
        RequireNear(playerStats.CurrentHealth, 80f, "Player trade damage");
        RequireNear(enemyStats.CurrentHealth, 80f, "Enemy trade damage");
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Trade,
            "Player trade outcome"
        );
        RequireEqual(
            enemy.FrameRunner.LastOutcome,
            CombatFrameOutcome.Trade,
            "Enemy trade outcome"
        );
    }

    private static void ValidateBufferExpiry()
    {
        ResetScenario();
        RequireStarted(player.HeavyAttack(), "Buffered source attack");
        Advance(1);
        RequireStarted(player.LightAttack(), "Buffered replacement");
        RequireEqual(
            player.FrameRunner.BufferedCommand,
            CombatActionId.AttackA,
            "Buffered action"
        );
        Advance(6);
        RequireEqual(
            player.FrameRunner.BufferedCommand,
            CombatActionId.None,
            "Expired buffer was cleared"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Expired,
            "Buffer expiry outcome"
        );
    }

    private static void ValidateParry()
    {
        ResetScenario();
        RequireStarted(enemy.LightAttack(), "Parry source attack");
        Advance(6);
        RequireStarted(player.StartDefense(), "Parry input");
        Advance(2);
        RequireNear(
            playerStats.CurrentHealth,
            100f,
            "Parry prevented damage"
        );
        RequireNear(
            playerStats.CurrentStamina,
            95f,
            "Parry cost and refund"
        );
        Require(
            player.FrameRunner.IsRiposteWindowActive,
            "Parry did not open the riposte window."
        );
        RequireEqual(
            player.FrameRunner.RiposteRemaining,
            30,
            "Riposte window length"
        );
    }

    private static void ValidateHeldGuardAndGuardBreak()
    {
        ResetScenario();
        RequireStarted(player.StartHeldGuard(), "Held guard");
        Advance(1);
        RequireNear(
            playerStats.CurrentStamina,
            100f,
            "Held guard passive cost"
        );
        RequireStarted(enemy.LightAttack(), "Blocked attack");
        Advance(8);
        RequireNear(
            playerStats.CurrentStamina,
            85f,
            "Blocked impact stamina cost"
        );
        RequireEqual(
            player.FrameRunner.BlockstunRemaining,
            9,
            "Attack A blockstun"
        );

        ResetScenario();
        playerStats.SetStamina(15f);
        RequireStarted(player.StartHeldGuard(), "Guard-break guard");
        Advance(1);
        RequireStarted(enemy.LightAttack(), "Guard-break attack");
        Advance(8);
        RequireEqual(
            player.FrameRunner.GuardBreakRemaining,
            240,
            "Guard-break duration"
        );
        Advance(243);
        RequireEqual(
            player.FrameRunner.GuardBreakRemaining,
            0,
            "Guard-break completion"
        );
        RequireNear(
            playerStats.CurrentStamina,
            15f,
            "Guard-break stamina recovery"
        );
    }

    private static void ValidateInterruptedDodge()
    {
        ResetScenario();
        RequireStarted(enemy.LightAttack(), "Dodge interruption attack");
        Advance(6);
        Vector3 startPosition = player.transform.position;
        RequireStarted(player.DodgeLeft(), "Interrupted dodge");
        Advance(2);
        RequireNear(
            playerStats.CurrentHealth,
            80f,
            "Pre-invulnerability dodge damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.InterruptedDodge,
            "Interrupted dodge outcome"
        );
        Require(
            !spatial.HasPendingDodge,
            "Interrupted dodge transaction survived."
        );
        Require(
            Vector3.Distance(
                startPosition,
                player.transform.position
            ) > 0.001f,
            "Interrupted dodge rolled back to its start."
        );
        bool hasPlayerPose = spatial.TryGetNeutralPose(
            player,
            out Pose playerPose
        );
        bool hasEnemyPose = spatial.TryGetNeutralPose(
            enemy,
            out Pose enemyPose
        );
        Require(
            hasPlayerPose && hasEnemyPose,
            "Interrupted dodge neutral poses are unavailable."
        );
        Quaternion expectedPlayerRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(
                enemyPose.position - playerPose.position,
                Vector3.up
            ).normalized,
            Vector3.up
        );
        Quaternion expectedEnemyRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(
                playerPose.position - enemyPose.position,
                Vector3.up
            ).normalized,
            Vector3.up
        );
        Require(
            Quaternion.Angle(
                playerPose.rotation,
                expectedPlayerRotation
            ) <= Tolerance,
            "Interrupted dodger is not centred on the opponent."
        );
        Require(
            Quaternion.Angle(
                enemyPose.rotation,
                expectedEnemyRotation
            ) <= Tolerance,
            "Interrupted dodge opponent is not centred on the dodger."
        );
    }

    private static void ValidateBlockClearsBuffer()
    {
        ResetScenario();
        RequireStarted(player.StartHeldGuard(), "Buffered block guard");
        RequireStarted(enemy.LightAttack(), "Buffered block attack");
        Advance(4);
        RequireStarted(player.LightAttack(), "Command buffered before block");
        RequireEqual(
            player.FrameRunner.BufferedCommand,
            CombatActionId.AttackA,
            "Pre-block buffered action"
        );
        Advance(4);
        RequireEqual(
            player.FrameRunner.BufferedCommand,
            CombatActionId.None,
            "Blockstun cleared the command buffer"
        );
    }

    private static void ValidateDistanceDodgeJumpArc()
    {
        ResetScenario();
        float forwardGroundY = player.transform.position.y;
        cameraController.ResetCameraView(true);
        float forwardCameraY = Camera.main.transform.position.y;
        RequireStarted(
            player.DodgeForward(),
            "Forward jump dodge"
        );
        Advance(9);
        cameraController.ResetCameraView(true);
        Require(
            player.transform.position.y > forwardGroundY + 0.25f,
            "Forward dodge did not rise into a jump arc."
        );
        RequireNear(
            Camera.main.transform.position.y,
            forwardCameraY,
            "Forward jump camera height"
        );
        Advance(11);
        RequireNear(
            player.transform.position.y,
            forwardGroundY,
            "Forward jump landing height"
        );
        RequireEqual(
            spatial.CurrentDistance,
            DistanceLevel.CloseRange,
            "Forward jump distance"
        );

        ResetScenario();
        float backwardGroundY = player.transform.position.y;
        cameraController.ResetCameraView(true);
        float backwardCameraY = Camera.main.transform.position.y;
        RequireStarted(
            player.DodgeBackward(),
            "Backward jump dodge"
        );
        Advance(9);
        cameraController.ResetCameraView(true);
        Require(
            player.transform.position.y > backwardGroundY + 0.25f,
            "Backward dodge did not rise into a jump arc."
        );
        RequireNear(
            Camera.main.transform.position.y,
            backwardCameraY,
            "Backward jump camera height"
        );
        Advance(11);
        RequireNear(
            player.transform.position.y,
            backwardGroundY,
            "Backward jump landing height"
        );
        RequireEqual(
            spatial.CurrentDistance,
            DistanceLevel.LongRange,
            "Backward jump distance"
        );
    }

    private static void ValidateDodgeWindows()
    {
        ResetScenario();
        RequireStarted(player.DodgeLeft(), "Invulnerable dodge");
        RequireStarted(enemy.LightAttack(), "Dodge source attack");
        Advance(8);
        RequireNear(
            playerStats.CurrentHealth,
            100f,
            "Invulnerable dodge prevented damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Dodge,
            "Invulnerable dodge outcome"
        );

        ResetScenario();
        RequireStarted(player.DodgeForward(), "Perfect dodge");
        Advance(2);
        RequireStarted(enemy.LightAttack(), "Perfect-dodge attack");
        Advance(8);
        RequireNear(
            playerStats.CurrentHealth,
            100f,
            "Perfect dodge prevented damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.PerfectDodge,
            "Perfect dodge outcome"
        );
        Require(
            enemy.FrameRunner.HitstunRemaining > 0,
            "Perfect dodge reward did not stun the attacker."
        );
    }

    private static void ValidatePermutation()
    {
        ResetScenario();
        playerStats.SetStamina(50f);
        RelativeOrientation orientationBefore =
            spatial.CurrentOrientation;
        RequireStarted(
            player.TryPermutation(70001),
            "Exact-cost permutation"
        );
        Advance(4);
        RequireNear(
            playerStats.CurrentStamina,
            0f,
            "Permutation exact cost"
        );
        RequireEqual(
            spatial.CurrentDistance,
            DistanceLevel.LongRange,
            "Permutation distance cycle"
        );
        RequireEqual(
            spatial.CurrentOrientation,
            orientationBefore,
            "Permutation orientation preservation"
        );
        RequireEqual(
            player.FrameRunner.GuardBreakRemaining,
            0,
            "Permutation zero stamina is not guard break"
        );
    }

    private static void ValidatePermutationInvulnerability()
    {
        ResetScenario();
        RequireStarted(
            enemy.LightAttack(),
            "Permutation invulnerability source attack"
        );
        Advance(5);
        playerStats.SetStamina(50f);
        RequireStarted(
            player.TryPermutation(70002),
            "Permutation invulnerability"
        );
        Advance(3);
        RequireNear(
            playerStats.CurrentHealth,
            100f,
            "Permutation invulnerability prevented damage"
        );
        RequireEqual(
            player.FrameRunner.LastOutcome,
            CombatFrameOutcome.Dodge,
            "Permutation invulnerability outcome"
        );
    }

    private static void ValidateFlankTimer()
    {
        ResetScenario();
        RequireStarted(player.DodgeLeft(), "Flank dodge");
        Advance(26);
        Require(
            spatial.CurrentOrientation is
                RelativeOrientation.LeftFlank or
                RelativeOrientation.RightFlank,
            "Lateral dodge did not create a flank."
        );
        int remaining = Mathf.Max(
            0,
            180 - spatial.FlankElapsedFrames
        );
        if (remaining > 1)
            Advance(remaining - 1);
        Require(
            spatial.CurrentOrientation != RelativeOrientation.Face,
            "Flank auto-face occurred before frame 180."
        );
        Advance(1);
        RequireEqual(
            spatial.CurrentOrientation,
            RelativeOrientation.Face,
            "Flank auto-face at frame 180"
        );
    }

    private static void ValidateInfiniteStamina()
    {
        ResetScenario();
        playerStats.SetInfiniteStamina(true);
        RequireStarted(player.HeavyAttack(), "Infinite-stamina attack");
        Advance(1);
        RequireNear(
            playerStats.CurrentStamina,
            100f,
            "Infinite stamina remains full"
        );
        playerStats.SetInfiniteStamina(false);
    }

    private static void ValidateReplayReset()
    {
        ResetScenario();
        RequireStarted(player.HeavyAttack(), "Reset source attack");
        Advance(1);
        RequireStarted(player.LightAttack(), "Reset buffered attack");
        frameSystem.ResetSystem();
        clock.StopClock();
        spatial.ResetDuel();
        RequireEqual(clock.CurrentFrame, 0, "Reset clock");
        RequireEqual(
            player.FrameRunner.CurrentActionId,
            CombatActionId.None,
            "Reset current action"
        );
        RequireEqual(
            player.FrameRunner.BufferedCommand,
            CombatActionId.None,
            "Reset buffer"
        );
        Require(
            !spatial.HasPendingDodge &&
            spatial.CurrentDistance == DistanceLevel.MidRange &&
            spatial.CurrentOrientation == RelativeOrientation.Face,
            "Reset spatial state"
        );
    }

    private static void ValidateFlightRecorder()
    {
        ResetScenario();
        traceRecorder.RecordSystemEvent(
            "VALIDATION_TRACE_PRE_MARKER"
        );
        RequireStarted(
            player.DodgeRight(),
            "Flight recorder dodge"
        );
        Advance(8);

        Require(
            traceRecorder.CaptureReport(),
            "Flight recorder did not start a capture."
        );
        Advance(traceRecorder.PostCaptureFrames + 1);

        Require(
            !traceRecorder.CapturePending,
            "Flight recorder capture did not finish."
        );
        string path = traceRecorder.LastSavedTracePath;
        Require(
            !string.IsNullOrEmpty(path) && File.Exists(path),
            "Flight recorder report was not written."
        );

        try
        {
            string report = File.ReadAllText(path);
            Require(
                report.Contains("BUG_MARKER_USER_REQUESTED"),
                "Flight recorder report has no user marker."
            );
            Require(
                report.Contains("FRAME|"),
                "Flight recorder report has no frame samples."
            );
            Require(
                report.Contains("category=SPATIAL"),
                "Flight recorder report has no spatial events."
            );
            Require(
                report.Contains("source=CombatSpatialController"),
                "Flight recorder report has no movement source."
            );
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void ResetScenario()
    {
        clock.StopClock();
        playerStats.SetInfiniteStamina(false);
        enemyStats.SetInfiniteStamina(false);
        playerStats.ResetStats();
        enemyStats.ResetStats();
        player.ResetCombatState();
        enemy.ResetCombatState();
        spatial.ResetDuel();
        spatial.SetCombatEnabled(true);
        frameSystem.ResetSystem();
        clock.StopClock();
    }

    private static void Advance(int frames)
    {
        frameSystem.AdvanceFramesForTests(frames);
    }

    private static void RequireStarted(
        CombatActionResult result,
        string label)
    {
        RequireEqual(
            result,
            CombatActionResult.Started,
            label
        );
    }

    private static void RequireNear(
        float actual,
        float expected,
        string label)
    {
        Require(
            Mathf.Abs(actual - expected) <= Tolerance,
            $"{label}: expected {expected}, got {actual}."
        );
    }

    private static void RequireEqual<T>(
        T actual,
        T expected,
        string label)
    {
        Require(
            Equals(actual, expected),
            $"{label}: expected {expected}, got {actual}."
        );
    }

    private static T GetPrivateField<T>(
        object target,
        string fieldName)
    {
        FieldInfo field = target?.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Field {fieldName} was not found."
            );
        }

        return (T)field.GetValue(target);
    }

    private static void SetPrivateField<T>(
        object target,
        string fieldName,
        T value)
    {
        FieldInfo field = target?.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Field {fieldName} was not found."
            );
        }

        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
