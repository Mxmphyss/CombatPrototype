using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatTraceRecorder : MonoBehaviour
{
    private const float PositionEpsilon = 0.0001f;
    private const float RotationEpsilon = 0.05f;

    [Header("Historique")]
    [Min(5f)]
    [SerializeField] private float historySeconds = 15f;
    [Min(0f)]
    [SerializeField] private float postCaptureSeconds = 2f;
    [Min(1000)]
    [SerializeField] private int maximumHistoryRecords = 12000;

    private readonly Queue<TraceEntry> history = new();
    private readonly List<string> activeCapture = new();
    private readonly StringBuilder builder = new(1024);

    private FighterCombat player;
    private FighterCombat enemy;
    private FighterStats playerStats;
    private FighterStats enemyStats;
    private CombatFrameSystem frameSystem;
    private CombatSpatialController spatial;
    private CombatGestureGrid gestureGrid;
    private EnemyAutoCombat enemyAI;
    private Camera combatCamera;
    private bool initialized;
    private bool capturePending;
    private int postCaptureFramesRemaining;
    private double captureDueRealtime;
    private Vector3 previousPlayerPosition;
    private Quaternion previousPlayerRotation;
    private Vector3 previousEnemyPosition;
    private Quaternion previousEnemyRotation;
    private Vector3 previousCameraPosition;
    private Quaternion previousCameraRotation;
    private string previousGestureZones = string.Empty;

    public event Action<bool> OnCaptureStateChanged;
    public event Action<string> OnReportSaved;

    public bool CapturePending => capturePending;
    public string LastSavedTracePath { get; private set; } =
        string.Empty;
    public int PostCaptureFrames => Mathf.Max(
        0,
        Mathf.RoundToInt(
            postCaptureSeconds *
            (frameSystem?.Settings?.FramesPerSecond ??
             CombatFrameClock.DefaultFramesPerSecond)
        )
    );

    public void Initialize(
        FighterCombat playerFighter,
        FighterCombat enemyFighter,
        CombatFrameSystem deterministicFrameSystem,
        CombatSpatialController spatialAuthority,
        CombatGestureGrid gestureInput,
        EnemyAutoCombat enemyController,
        Camera targetCamera)
    {
        Unsubscribe();

        player = playerFighter;
        enemy = enemyFighter;
        playerStats = player != null ? player.Stats : null;
        enemyStats = enemy != null ? enemy.Stats : null;
        frameSystem = deterministicFrameSystem;
        spatial = spatialAuthority;
        gestureGrid = gestureInput;
        enemyAI = enemyController;
        combatCamera = targetCamera;
        initialized =
            player != null &&
            enemy != null &&
            frameSystem != null &&
            spatial != null;

        RememberTransforms();
        Subscribe();
        RecordSystemEvent("RECORDER_INITIALIZED");
    }

    private void Update()
    {
        if (capturePending &&
            Time.realtimeSinceStartupAsDouble >= captureDueRealtime)
        {
            SavePendingCapture();
        }
    }

    private void OnDestroy()
    {
        if (capturePending)
            SavePendingCapture();
        Unsubscribe();
    }

    public bool CaptureReport()
    {
        if (!initialized || capturePending)
            return false;

        activeCapture.Clear();
        foreach (TraceEntry entry in history)
            activeCapture.Add(entry.Line);

        capturePending = true;
        postCaptureFramesRemaining = PostCaptureFrames;
        captureDueRealtime =
            Time.realtimeSinceStartupAsDouble +
            Mathf.Max(0f, postCaptureSeconds);
        RecordSystemEvent("BUG_MARKER_USER_REQUESTED");
        OnCaptureStateChanged?.Invoke(true);

        if (postCaptureFramesRemaining <= 0)
            SavePendingCapture();

        return true;
    }

    public void RecordSystemEvent(string message)
    {
        RecordEvent("SYSTEM", message);
    }

    private void Subscribe()
    {
        if (!initialized)
            return;

        frameSystem.OnFrameCompleted += HandleFrameCompleted;
        frameSystem.OnOutcome += HandleOutcome;
        spatial.OnTelemetry += HandleSpatialTelemetry;
        player.OnStateChanged += HandleFighterStateChanged;
        enemy.OnStateChanged += HandleFighterStateChanged;
        player.OnAttackResolved += HandleAttackResolved;
        enemy.OnAttackResolved += HandleAttackResolved;
        player.OnGuardImpact += HandleGuardImpact;
        enemy.OnGuardImpact += HandleGuardImpact;

        if (playerStats != null)
        {
            playerStats.OnHealthChanged += HandlePlayerHealth;
            playerStats.OnStaminaChanged += HandlePlayerStamina;
        }
        if (enemyStats != null)
        {
            enemyStats.OnHealthChanged += HandleEnemyHealth;
            enemyStats.OnStaminaChanged += HandleEnemyStamina;
        }
        if (gestureGrid != null)
        {
            gestureGrid.GestureStarted += HandleGesture;
            gestureGrid.GestureUpdated += HandleGesture;
            gestureGrid.GestureCompleted += HandleGesture;
            gestureGrid.GestureFailed += HandleGesture;
        }
        if (enemyAI != null)
            enemyAI.OnAIEnabledChanged += HandleAIEnabledChanged;

        Application.logMessageReceived += HandleUnityLog;
    }

    private void Unsubscribe()
    {
        if (frameSystem != null)
        {
            frameSystem.OnFrameCompleted -= HandleFrameCompleted;
            frameSystem.OnOutcome -= HandleOutcome;
        }
        if (spatial != null)
            spatial.OnTelemetry -= HandleSpatialTelemetry;
        if (player != null)
        {
            player.OnStateChanged -= HandleFighterStateChanged;
            player.OnAttackResolved -= HandleAttackResolved;
            player.OnGuardImpact -= HandleGuardImpact;
        }
        if (enemy != null)
        {
            enemy.OnStateChanged -= HandleFighterStateChanged;
            enemy.OnAttackResolved -= HandleAttackResolved;
            enemy.OnGuardImpact -= HandleGuardImpact;
        }
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= HandlePlayerHealth;
            playerStats.OnStaminaChanged -= HandlePlayerStamina;
        }
        if (enemyStats != null)
        {
            enemyStats.OnHealthChanged -= HandleEnemyHealth;
            enemyStats.OnStaminaChanged -= HandleEnemyStamina;
        }
        if (gestureGrid != null)
        {
            gestureGrid.GestureStarted -= HandleGesture;
            gestureGrid.GestureUpdated -= HandleGesture;
            gestureGrid.GestureCompleted -= HandleGesture;
            gestureGrid.GestureFailed -= HandleGesture;
        }
        if (enemyAI != null)
            enemyAI.OnAIEnabledChanged -= HandleAIEnabledChanged;

        Application.logMessageReceived -= HandleUnityLog;
    }

    private void HandleFrameCompleted(int globalFrame)
    {
        RecordFrame(globalFrame);
        RememberTransforms();

        if (!capturePending)
            return;

        postCaptureFramesRemaining--;
        if (postCaptureFramesRemaining <= 0)
            SavePendingCapture();
    }

    private void RecordFrame(int globalFrame)
    {
        CombatSpatialSnapshot snapshot = spatial.Snapshot;
        CombatFrameTelemetry playerFrame =
            player.FrameRunner?.CreateTelemetry() ?? default;
        CombatFrameTelemetry enemyFrame =
            enemy.FrameRunner?.CreateTelemetry() ?? default;

        Vector3 playerPosition = player.transform.position;
        Quaternion playerRotation = player.transform.rotation;
        Vector3 enemyPosition = enemy.transform.position;
        Quaternion enemyRotation = enemy.transform.rotation;
        Vector3 cameraPosition = combatCamera != null
            ? combatCamera.transform.position
            : Vector3.zero;
        Quaternion cameraRotation = combatCamera != null
            ? combatCamera.transform.rotation
            : Quaternion.identity;

        builder.Clear();
        builder.Append("FRAME|f=").Append(globalFrame);
        builder.Append("|t=").Append(F(Time.unscaledTime));
        AppendFighter(
            "P",
            player,
            playerStats,
            playerFrame,
            playerPosition,
            playerRotation,
            snapshot.FirstNeutralPose,
            playerPosition - previousPlayerPosition,
            Quaternion.Angle(
                previousPlayerRotation,
                playerRotation
            ),
            snapshot.FirstMovement,
            snapshot.HasPendingDodge
        );
        AppendFighter(
            "E",
            enemy,
            enemyStats,
            enemyFrame,
            enemyPosition,
            enemyRotation,
            snapshot.SecondNeutralPose,
            enemyPosition - previousEnemyPosition,
            Quaternion.Angle(
                previousEnemyRotation,
                enemyRotation
            ),
            snapshot.SecondMovement,
            snapshot.HasPendingDodge
        );
        builder.Append("|SPACE{distance=")
            .Append(snapshot.Distance)
            .Append(";orientation=")
            .Append(snapshot.Orientation)
            .Append(";separation=")
            .Append(F(snapshot.Separation))
            .Append(";revision=")
            .Append(snapshot.Revision)
            .Append(";pendingDodge=")
            .Append(snapshot.PendingDodgeId)
            .Append(";advantage=")
            .Append(FighterLabel(snapshot.AdvantageFighter))
            .Append('}');
        builder.Append("|AI{enabled=")
            .Append(enemyAI != null && enemyAI.EnemyAIEnabled)
            .Append(";telegraph=")
            .Append(enemyAI != null && enemyAI.IsAttackTelegraphing)
            .Append(";attack=")
            .Append(enemyAI != null
                ? enemyAI.TelegraphedAttack
                : CombatActionId.None)
            .Append(";remaining=")
            .Append(enemyAI != null
                ? enemyAI.AttackTelegraphRemainingFrames
                : 0)
            .Append('}');
        builder.Append("|CAM{pos=")
            .Append(V(cameraPosition))
            .Append(";rot=")
            .Append(V(cameraRotation.eulerAngles))
            .Append(";delta=")
            .Append(V(cameraPosition - previousCameraPosition))
            .Append(";rotDelta=")
            .Append(F(Quaternion.Angle(
                previousCameraRotation,
                cameraRotation
            )))
            .Append('}');

        RecordLine(builder.ToString());
    }

    private void AppendFighter(
        string label,
        FighterCombat fighter,
        FighterStats stats,
        CombatFrameTelemetry frame,
        Vector3 position,
        Quaternion rotation,
        Pose neutralPose,
        Vector3 delta,
        float rotationDelta,
        SpatialMovementType movement,
        bool pendingDodge)
    {
        builder.Append('|').Append(label).Append("{state=")
            .Append(fighter.CurrentState)
            .Append(";action=").Append(frame.CurrentAction)
            .Append(";phase=").Append(frame.CurrentPhase)
            .Append(";local=").Append(frame.LocalActionFrame)
            .Append(";outcome=").Append(frame.LastOutcome)
            .Append(";hp=").Append(F(stats?.CurrentHealth ?? 0f))
            .Append(";stamina=").Append(F(stats?.CurrentStamina ?? 0f))
            .Append(";pos=").Append(V(position))
            .Append(";rot=").Append(V(rotation.eulerAngles))
            .Append(";neutralPos=").Append(V(neutralPose.position))
            .Append(";neutralRot=")
            .Append(V(neutralPose.rotation.eulerAngles))
            .Append(";delta=").Append(V(delta))
            .Append(";rotDelta=").Append(F(rotationDelta))
            .Append(";source=")
            .Append(InferMovementSource(
                frame,
                delta,
                rotationDelta,
                movement,
                pendingDodge
            ))
            .Append(";invulnerable=").Append(frame.Invulnerable)
            .Append(";destinationValidated=")
            .Append(frame.DestinationValidated)
            .Append(";dodgeInterrupted=")
            .Append(frame.DodgeInterrupted)
            .Append('}');
    }

    private static string InferMovementSource(
        CombatFrameTelemetry frame,
        Vector3 delta,
        float rotationDelta,
        SpatialMovementType movement,
        bool pendingDodge)
    {
        bool moved = delta.sqrMagnitude > PositionEpsilon ||
                     rotationDelta > RotationEpsilon;
        if (!moved)
            return "None";
        if (CombatActionRunner.IsAttack(frame.CurrentAction))
            return "CombatActionRunner.AttackLunge";
        if (frame.CurrentPhase == CombatActionPhase.Dodging ||
            pendingDodge)
        {
            return "CombatSpatialController.DodgePreview";
        }
        if (frame.CurrentAction == CombatActionId.Permutation)
            return "CombatSpatialController.Permutation";
        if (movement != SpatialMovementType.None)
            return "CombatSpatialController.ContinuousMovement";
        if (frame.CurrentPhase is CombatActionPhase.Hitstun or
            CombatActionPhase.Blockstun or
            CombatActionPhase.GuardBrokenStun)
        {
            return "CombatActionRunner.ImpactRecovery";
        }
        return "UNEXPECTED_OR_EXTERNAL_TRANSFORM";
    }

    private void HandleOutcome(
        CombatActionRunner runner,
        CombatFrameOutcome outcome)
    {
        RecordEvent(
            "OUTCOME",
            $"fighter={FighterLabel(runner?.Owner)};" +
            $"action={runner?.CurrentActionId};outcome={outcome}"
        );
    }

    private void HandleSpatialTelemetry(
        CombatSpatialTelemetry telemetry)
    {
        CombatSpatialSnapshot snapshot = telemetry.Snapshot;
        RecordEvent(
            "SPATIAL",
            $"reason={telemetry.Reason};" +
            $"instigator={FighterLabel(telemetry.Instigator)};" +
            $"dodge={telemetry.DodgeTransactionId};" +
            $"distance={snapshot.Distance};" +
            $"orientation={snapshot.Orientation};" +
            $"revision={snapshot.Revision};" +
            $"playerNeutral={V(snapshot.FirstNeutralPose.position)};" +
            $"enemyNeutral={V(snapshot.SecondNeutralPose.position)}"
        );
    }

    private void HandleFighterStateChanged(
        FighterCombat fighter,
        FighterCombatState state)
    {
        RecordEvent(
            "STATE",
            $"fighter={FighterLabel(fighter)};state={state};" +
            $"stun={fighter.CurrentStunReason}"
        );
    }

    private void HandleAttackResolved(CombatImpact impact)
    {
        RecordEvent(
            "IMPACT",
            $"attacker={FighterLabel(impact.Attacker)};" +
            $"target={FighterLabel(impact.Target)};" +
            $"result={impact.Result};" +
            $"orientation={impact.Orientation};" +
            $"multiplier={F(impact.PositionalMultiplier)};" +
            $"damage={F(impact.DamageApplied)}"
        );
    }

    private void HandleGuardImpact(GuardImpact impact)
    {
        RecordEvent(
            "GUARD",
            $"target={FighterLabel(impact.Target)};" +
            $"staminaDamage={F(impact.StaminaDamage)};" +
            $"broken={impact.GuardBroken}"
        );
    }

    private void HandleGesture(GestureDebugEventData data)
    {
        string zones = Zones(data.Zones);
        if (data.Phase == GestureDebugPhase.Updated &&
            zones == previousGestureZones)
        {
            return;
        }

        previousGestureZones = zones;
        RecordEvent(
            "GESTURE",
            $"phase={data.Phase};kind={data.InputKind};" +
            $"zones={zones};raw={Zones(data.RawZones)};" +
            $"normalized={Zones(data.NormalizedZones)};" +
            $"gesture={data.GestureId};status={data.RecognitionStatus};" +
            $"mapped={data.IsActionMapped};action={data.ActionLabel};" +
            $"result={(data.HasCombatResult ? data.CombatResult.ToString() : "Pending")};" +
            $"refusal={data.RefusalReason}"
        );

        if (data.Phase is GestureDebugPhase.Completed or
            GestureDebugPhase.Failed)
        {
            previousGestureZones = string.Empty;
        }
    }

    private void HandlePlayerHealth(float current, float maximum) =>
        RecordEvent("STATS", $"fighter=Player;health={F(current)}/{F(maximum)}");

    private void HandleEnemyHealth(float current, float maximum) =>
        RecordEvent("STATS", $"fighter=Enemy;health={F(current)}/{F(maximum)}");

    private void HandlePlayerStamina(float current, float maximum) =>
        RecordEvent("STATS", $"fighter=Player;stamina={F(current)}/{F(maximum)}");

    private void HandleEnemyStamina(float current, float maximum) =>
        RecordEvent("STATS", $"fighter=Enemy;stamina={F(current)}/{F(maximum)}");

    private void HandleAIEnabledChanged(bool enabled)
    {
        RecordEvent("AI", $"enabled={enabled}");
    }

    private void HandleUnityLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is not LogType.Error and
            not LogType.Assert and
            not LogType.Exception)
        {
            return;
        }

        string compactCondition = Compact(condition);
        string compactStack = Compact(stackTrace);
        RecordEvent(
            "UNITY_ERROR",
            $"type={type};message={compactCondition};stack={compactStack}"
        );
    }

    private void RecordEvent(string category, string message)
    {
        int frame = frameSystem != null
            ? frameSystem.CurrentFrame
            : 0;
        RecordLine(
            $"EVENT|f={frame}|t={F(Time.unscaledTime)}|" +
            $"category={category}|{Compact(message)}"
        );
    }

    private void RecordLine(string line)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        history.Enqueue(new TraceEntry(now, line));
        while (history.Count > 0 &&
               (history.Count > Mathf.Max(1000, maximumHistoryRecords) ||
                now - history.Peek().Realtime >
                    Mathf.Max(5f, historySeconds)))
        {
            history.Dequeue();
        }

        if (capturePending)
            activeCapture.Add(line);
    }

    private void SavePendingCapture()
    {
        if (!capturePending)
            return;

        capturePending = false;
        postCaptureFramesRemaining = 0;
        string path = string.Empty;
        bool saved = false;

        try
        {
            string directory = Path.Combine(
                Application.persistentDataPath,
                "CombatTraces"
            );
            Directory.CreateDirectory(directory);
            path = Path.Combine(
                directory,
                "combat-trace-" +
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss-fff",
                    CultureInfo.InvariantCulture
                ) +
                ".txt"
            );

            builder.Clear();
            builder.AppendLine("COMBAT PROTOTYPE FLIGHT RECORDER");
            builder.Append("created=")
                .AppendLine(DateTime.Now.ToString("O"));
            builder.Append("platform=")
                .AppendLine(Application.platform.ToString());
            builder.Append("unity=")
                .AppendLine(Application.unityVersion);
            builder.Append("device=")
                .AppendLine(SystemInfo.deviceModel);
            builder.Append("persistentDataPath=")
                .AppendLine(Application.persistentDataPath);
            builder.Append("historySeconds=")
                .AppendLine(F(historySeconds));
            builder.Append("postCaptureSeconds=")
                .AppendLine(F(postCaptureSeconds));
            builder.AppendLine("--- TRACE ---");
            foreach (string record in activeCapture)
                builder.AppendLine(record);

            File.WriteAllText(
                path,
                builder.ToString(),
                new UTF8Encoding(false)
            );
            LastSavedTracePath = path;
            saved = true;
            Debug.Log($"[CombatTrace] Rapport sauvegarde : {path}");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[CombatTrace] Echec de sauvegarde : " + exception
            );
        }
        finally
        {
            activeCapture.Clear();
            OnCaptureStateChanged?.Invoke(false);
            if (saved)
                OnReportSaved?.Invoke(path);
        }
    }

    private void RememberTransforms()
    {
        if (player != null)
        {
            previousPlayerPosition = player.transform.position;
            previousPlayerRotation = player.transform.rotation;
        }
        if (enemy != null)
        {
            previousEnemyPosition = enemy.transform.position;
            previousEnemyRotation = enemy.transform.rotation;
        }
        if (combatCamera != null)
        {
            previousCameraPosition = combatCamera.transform.position;
            previousCameraRotation = combatCamera.transform.rotation;
        }
    }

    private static string FighterLabel(FighterCombat fighter)
    {
        if (fighter == null)
            return "None";
        return fighter.IsPlayerControlled ? "Player" : "Enemy";
    }

    private static string Zones(
        IReadOnlyList<int> zones)
    {
        if (zones == null || zones.Count == 0)
            return "-";

        StringBuilder result = new();
        for (int index = 0; index < zones.Count; index++)
        {
            if (index > 0)
                result.Append('>');
            int zone = zones[index];
            result.Append(
                zone >= 0 && zone < 9
                    ? ((char)('A' + zone)).ToString()
                    : zone.ToString(CultureInfo.InvariantCulture)
            );
        }
        return result.ToString();
    }

    private static string V(Vector3 value) =>
        $"({F(value.x)},{F(value.y)},{F(value.z)})";

    private static string F(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Compact(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('|', '/');
    }

    private readonly struct TraceEntry
    {
        public double Realtime { get; }
        public string Line { get; }

        public TraceEntry(double realtime, string line)
        {
            Realtime = realtime;
            Line = line;
        }
    }
}
