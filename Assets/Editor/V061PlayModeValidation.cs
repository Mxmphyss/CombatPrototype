using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class V061PlayModeValidation
{
    private const string ActiveKey =
        "CombatPrototype.V061PlayMode.Active";
    private const string ResultKey =
        "CombatPrototype.V061PlayMode.Result";
    private const string ScenePath =
        "Assets/Scenes/CombatArena.unity";
    private const float PositionTolerance = 0.01f;

    private static double stageStartedAt;
    private static int stage;
    private static CombatSpatialController spatialController;
    private static CombatCameraController cameraController;
    private static CombatDistanceDebugVisualizer distanceVisualizer;
    private static FighterCombat player;
    private static FighterCombat opponent;
    private static Vector3 stationaryOpponentPosition;
    private static Quaternion stableCameraRotation;
    private static Vector2 stablePlayerViewport;

    static V061PlayModeValidation()
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
            throw new InvalidOperationException(
                "V061 PlayMode validation requires Edit Mode."
            );

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(ResultKey, 0);
        stage = 0;
        stageStartedAt = EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single
        );
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            stageStartedAt = EditorApplication.timeSinceStartup;
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
                "V061PlayModeValidation: runtime camera, dodge, " +
                "strafe, permutation and debug visual checks passed."
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
            switch (stage)
            {
                case 0:
                    WaitForRuntime();
                    break;
                case 1:
                    WaitForForwardDodge();
                    break;
                case 2:
                    WaitForStrafe();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void WaitForRuntime()
    {
        if (!TryResolveRuntime())
        {
            RequireNotTimedOut(
                20d,
                "Combat runtime did not initialize."
            );
            return;
        }

        EnemyAutoCombat enemyAI =
            UnityEngine.Object.FindFirstObjectByType<
                EnemyAutoCombat>();
        enemyAI?.SetAIEnabled(false);
        Require(
            spatialController.CurrentDistance ==
                DistanceLevel.MidRange &&
            spatialController.CurrentOrientation ==
                RelativeOrientation.Face,
            "Runtime duel did not start at MidRange/Face."
        );
        cameraController.ResetCameraView(true);
        RequirePairVisible("runtime start");
        stableCameraRotation =
            Camera.main.transform.rotation;

        CombatActionResult result = player.DodgeForward();
        Require(
            result == CombatActionResult.Started,
            "Runtime forward dodge was refused."
        );
        BeginStage(1);
    }

    private static void WaitForForwardDodge()
    {
        if (player.CurrentState != FighterCombatState.Idle)
        {
            RequireNotTimedOut(
                5d,
                "Runtime forward dodge did not finish."
            );
            return;
        }

        Require(
            spatialController.CurrentDistance ==
                DistanceLevel.CloseRange,
            "Runtime forward dodge did not commit CloseRange."
        );
        RequireNear(
            spatialController.Snapshot.Separation,
            spatialController.GetDistance(
                DistanceLevel.CloseRange
            ),
            "Runtime forward dodge missed its exact anchor."
        );
        cameraController.ResetCameraView(true);
        stableCameraRotation =
            Camera.main.transform.rotation;
        stablePlayerViewport =
            Camera.main.WorldToViewportPoint(
                player.transform.position
            );
        stationaryOpponentPosition =
            opponent.transform.position;
        Require(
            player.StartSpatialMovement(
                SpatialMovementType.StrafeLeft
            ) == CombatActionResult.Started,
            "Runtime player strafe was refused."
        );
        BeginStage(2);
    }

    private static void WaitForStrafe()
    {
        if (Elapsed < 0.75d)
            return;

        player.StopSpatialMovement();
        Require(
            Vector3.Distance(
                stationaryOpponentPosition,
                opponent.transform.position
            ) <= PositionTolerance,
            "Runtime strafe moved the stationary opponent."
        );
        Require(
            spatialController.CurrentDistance ==
                DistanceLevel.CloseRange &&
            spatialController.CurrentOrientation ==
                RelativeOrientation.Face,
            "Runtime strafe changed distance or Face orientation."
        );
        Require(
            Quaternion.Angle(
                stableCameraRotation,
                Camera.main.transform.rotation
            ) <= 0.1f,
            "Runtime camera rotated the map during player strafe."
        );
        RequirePlayerScreenAnchor("runtime strafe");
        cameraController.ResetCameraView(true);
        RequirePairVisible("runtime strafe");

        FighterStats playerStats =
            player.GetComponent<FighterStats>();
        playerStats.SetStamina(50f);
        Require(
            player.TryPermutation(61001) ==
                CombatActionResult.Started,
            "Runtime permutation was refused."
        );
        Require(
            spatialController.CurrentDistance ==
                DistanceLevel.MidRange &&
            spatialController.CurrentOrientation ==
                RelativeOrientation.Face,
            "Runtime permutation did not cycle Close to Mid/Face."
        );
        RequireNear(
            playerStats.CurrentStamina,
            0f,
            "Runtime permutation did not spend exactly 50."
        );
        Require(
            player.CurrentState != FighterCombatState.Stunned,
            "Runtime zero-stamina permutation caused a stun."
        );
        Require(
            distanceVisualizer
                .GetComponentsInChildren<Collider>(true)
                .Length == 0,
            "Runtime distance visualizer created a collider."
        );
        CompleteSuccessfully();
    }

    private static bool TryResolveRuntime()
    {
        spatialController ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatSpatialController>();
        cameraController ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatCameraController>();
        distanceVisualizer ??=
            UnityEngine.Object.FindFirstObjectByType<
                CombatDistanceDebugVisualizer>();
        if (player == null || opponent == null)
        {
            FighterCombat[] fighters =
                UnityEngine.Object.FindObjectsByType<FighterCombat>(
                    FindObjectsSortMode.None
                );
            player = fighters.FirstOrDefault(
                fighter => fighter.IsPlayerControlled
            );
            opponent = fighters.FirstOrDefault(
                fighter => !fighter.IsPlayerControlled
            );
        }

        return spatialController != null &&
            cameraController != null &&
            distanceVisualizer != null &&
            player != null &&
            opponent != null &&
            Camera.main != null;
    }

    private static void RequirePairVisible(string situation)
    {
        RequireVisible(
            player.transform.position,
            $"Player is outside the camera at {situation}."
        );
        RequireVisible(
            opponent.transform.position,
            $"Opponent is outside the camera at {situation}."
        );
    }

    private static void RequireCameraBehindPlayer(
        string situation)
    {
        Vector3 duelForward = Vector3.ProjectOnPlane(
            opponent.transform.position -
            player.transform.position,
            Vector3.up
        ).normalized;
        Vector3 cameraForward = Vector3.ProjectOnPlane(
            Camera.main.transform.forward,
            Vector3.up
        ).normalized;
        Vector3 playerToCamera = Vector3.ProjectOnPlane(
            Camera.main.transform.position -
            player.transform.position,
            Vector3.up
        ).normalized;

        Require(
            Vector3.Dot(cameraForward, duelForward) >= 0.99f,
            $"Camera does not face the opponent at {situation}."
        );
        Require(
            Vector3.Dot(playerToCamera, duelForward) <= -0.4f,
            $"Camera is not behind the player at {situation}."
        );
    }

    private static void RequirePlayerScreenAnchor(
        string situation)
    {
        Vector2 actualViewport =
            Camera.main.WorldToViewportPoint(
                player.transform.position
            );
        Require(
            Vector2.Distance(
                stablePlayerViewport,
                actualViewport
            ) <= PositionTolerance,
            $"Player screen anchor drifted at {situation}. " +
            $"Expected=({stablePlayerViewport.x:F3}, " +
            $"{stablePlayerViewport.y:F3}), " +
            $"Actual=({actualViewport.x:F3}, " +
            $"{actualViewport.y:F3})."
        );
    }

    private static void RequireVisible(
        Vector3 worldPosition,
        string message)
    {
        Vector3 viewport =
            Camera.main.WorldToViewportPoint(worldPosition);
        Require(
            viewport.z > 0f &&
            viewport.x >= 0f &&
            viewport.x <= 1f &&
            viewport.y >= 0f &&
            viewport.y <= 1f,
            message +
            $" Viewport=({viewport.x:F3}, " +
            $"{viewport.y:F3}, {viewport.z:F3})."
        );
    }

    private static void CompleteSuccessfully()
    {
        SessionState.SetInt(ResultKey, 1);
        EditorApplication.ExitPlaymode();
    }

    private static void Fail(Exception exception)
    {
        Debug.LogError(
            "V061PlayModeValidation failed: " + exception
        );
        SessionState.SetInt(ResultKey, -1);
        EditorApplication.ExitPlaymode();
    }

    private static void BeginStage(int nextStage)
    {
        stage = nextStage;
        stageStartedAt = EditorApplication.timeSinceStartup;
    }

    private static double Elapsed =>
        EditorApplication.timeSinceStartup - stageStartedAt;

    private static void RequireNotTimedOut(
        double timeout,
        string message)
    {
        Require(Elapsed <= timeout, message);
    }

    private static void RequireNear(
        float actual,
        float expected,
        string message)
    {
        Require(
            Mathf.Abs(actual - expected) <= PositionTolerance,
            $"{message} Expected {expected}, got {actual}."
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
