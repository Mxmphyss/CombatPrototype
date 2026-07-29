using System;
using System.Collections;
using UnityEngine;

public sealed class EnemyAutoCombat : MonoBehaviour
{
    [Header("Prototype Debug")]
    [SerializeField]
    private bool enemyAIEnabled = true;

    [Header("Rythme")]
    [Min(0f)]
    [SerializeField] private float minimumDelay = 1.5f;
    [Min(0f)]
    [SerializeField] private float maximumDelay = 2.5f;
    [Min(0f)]
    [SerializeField] private float telegraphDuration = 0.6f;

    [Header("Anticipation visuelle")]
    [Range(0f, 0.15f)]
    [SerializeField] private float pulseStrength = 0.035f;
    [Min(1f)]
    [SerializeField] private float pulseSpeed = 18f;
    [Range(0f, 0.35f)]
    [SerializeField] private float telegraphScaleStrength = 0.14f;
    [Min(1f)]
    [SerializeField] private float telegraphPulseSpeed = 10f;

    private FighterCombat enemy;
    private FighterCombat player;
    private CombatHUD hud;
    private CombatSpatialController spatialController;
    private CombatFrameSystem frameSystem;
    private CombatFrameClock frameClock;
    private Coroutine combatRoutine;
    private Coroutine compensationRoutine;
    private Vector3 normalScale;
    private bool initialized;
    private bool appliedAIEnabled = true;
    private int routineGeneration;
    private long nextPermutationToken;
    private bool frameTickSubscribed;
    private int nextDecisionFrame;
    private int compensationDueFrame = -1;
    private long compensationEpoch;
    private int compensationRevision;
    private RelativeOrientation compensationOrientation;
    private FighterCombat compensationAdvantage;
    private DodgeDirection compensationDirection;
    private CombatActionId telegraphedAttack =
        CombatActionId.None;
    private int telegraphDueFrame = -1;

    public event Action<bool> OnAIEnabledChanged;

    public bool EnemyAIEnabled => enemyAIEnabled;
    public bool IsAttackTelegraphing =>
        telegraphedAttack != CombatActionId.None &&
        telegraphDueFrame >= 0;
    public CombatActionId TelegraphedAttack => telegraphedAttack;
    public int AttackTelegraphDurationFrames =>
        frameClock != null
            ? Mathf.Max(
                1,
                Mathf.RoundToInt(
                    telegraphDuration *
                    frameClock.FramesPerSecond
                )
            )
            : 0;
    public int AttackTelegraphRemainingFrames =>
        IsAttackTelegraphing && frameClock != null
            ? Mathf.Max(
                0,
                telegraphDueFrame - frameClock.CurrentFrame
            )
            : 0;

    public void Initialize(
        FighterCombat enemyCombat,
        FighterCombat playerCombat,
        CombatHUD combatHud,
        CombatSpatialController spatialAuthority = null,
        CombatFrameSystem deterministicFrameSystem = null)
    {
        UnsubscribeSpatialEvents();
        StopAI();

        enemy = enemyCombat;
        player = playerCombat;
        hud = combatHud;
        spatialController = spatialAuthority;
        frameSystem = deterministicFrameSystem;
        frameClock = frameSystem != null
            ? frameSystem.Clock
            : null;
        normalScale = transform.localScale;
        initialized = true;
        appliedAIEnabled = enemyAIEnabled;
        SubscribeSpatialEvents();

        StartAI();
    }

    private void Update()
    {
        if (appliedAIEnabled != enemyAIEnabled)
            ApplyAIEnabledState();

        if (frameClock != null)
            UpdateFrameDrivenVisuals();
    }

    private void OnDisable()
    {
        StopAI();
    }

    private void OnDestroy()
    {
        UnsubscribeSpatialEvents();
    }

    public void StartAI()
    {
        if (frameClock != null)
        {
            if (!initialized || !enemyAIEnabled || !CanContinue())
                return;

            SubscribeFrameClock();
            ScheduleNextDecision(frameClock.CurrentFrame);
            return;
        }

        if (!initialized ||
            !enemyAIEnabled ||
            combatRoutine != null ||
            !CanContinue())
        {
            return;
        }

        combatRoutine = StartCoroutine(CombatLoop());
    }

    public void StopAI()
    {
        routineGeneration++;
        UnsubscribeFrameClock();
        compensationDueFrame = -1;
        CancelFrameAttackTelegraph();

        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        if (compensationRoutine != null)
        {
            StopCoroutine(compensationRoutine);
            compensationRoutine = null;
        }

        if (enemy != null)
        {
            enemy.StopChargeInput();
            enemy.StopSpatialMovement();
        }

        RestoreScale();
        if (hud != null)
            hud.SetEnemyStatus(string.Empty);
    }

    public void RestartAI()
    {
        StopAI();
        StartAI();
    }

    public void SetAIEnabled(bool enabled)
    {
        enemyAIEnabled = enabled;
        ApplyAIEnabledState();
    }

    private IEnumerator CombatLoop()
    {
        int generation = routineGeneration;
        while (CanContinue())
        {
            yield return WaitUntilAIEnabled();

            if (!CanAct() || generation != routineGeneration)
            {
                yield return null;
                continue;
            }

            float delay = UnityEngine.Random.Range(
                Mathf.Min(minimumDelay, maximumDelay),
                Mathf.Max(minimumDelay, maximumDelay)
            );
            yield return WaitWhileCombatContinues(delay);

            if (!CanAct() || generation != routineGeneration)
                continue;

            while (enemy.IsBusy && CanContinue())
                yield return null;

            if (!CanAct() || generation != routineGeneration)
                continue;

            if (TryUseConfiguredPermutation())
            {
                yield return new WaitForSeconds(
                    enemy.Rules.PermutationFeedbackDuration
                );
                if (CanAct())
                    hud.SetEnemyStatus(string.Empty);
                continue;
            }

            if (enemy.Stats.CurrentStamina + Mathf.Epsilon <
                enemy.LightAttackStaminaCost)
            {
                yield return RechargeUntilAttackIsAvailable();
            }

            if (!CanAct() || generation != routineGeneration)
                continue;

            CombatActionResult result =
                enemy.LightAttack(telegraphDuration);

            if (result == CombatActionResult.Started)
            {
                hud.SetEnemyStatus("Attaque");
                hud.ShowMessage(
                    "Attaque ennemie",
                    new Color(0.94f, 0.42f, 0.30f),
                    telegraphDuration
                );

                yield return PulseDuringStartup();

                while (enemy.IsBusy && CanContinue())
                    yield return null;
            }
            else if (
                result ==
                CombatActionResult.NotEnoughStamina)
            {
                yield return RechargeUntilAttackIsAvailable();
            }

            RestoreScale();
            if (CanAct())
                hud.SetEnemyStatus(string.Empty);
        }

        RestoreScale();
        if (generation == routineGeneration)
            combatRoutine = null;
    }

    private IEnumerator PulseDuringStartup()
    {
        while (CanContinue() &&
               enemy.CurrentState ==
               FighterCombatState.AttackStartup)
        {
            float pulse =
                1f +
                Mathf.Sin(Time.time * pulseSpeed) *
                pulseStrength;
            transform.localScale = normalScale * pulse;
            yield return null;
        }

        RestoreScale();
    }

    private IEnumerator RechargeUntilAttackIsAvailable()
    {
        hud.SetEnemyStatus("Recharge");

        while (CanContinue() &&
               enemy.Stats.CurrentStamina + Mathf.Epsilon <
               enemy.LightAttackStaminaCost)
        {
            if (enemy.CurrentState ==
                FighterCombatState.Stunned)
            {
                yield return null;
                continue;
            }

            if (!enemyAIEnabled)
            {
                yield return null;
                continue;
            }

            if (!enemy.IsCharging)
            {
                CombatActionResult result = enemy.StartCharge();
                if (result != CombatActionResult.Started)
                {
                    yield return null;
                    continue;
                }
            }

            yield return null;
        }

        if (enemyAIEnabled)
            enemy.StopChargeInput();

        if (CanAct())
        {
            hud.SetEnemyStatus(string.Empty);
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator WaitWhileCombatContinues(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && CanContinue())
        {
            if (enemyAIEnabled)
                elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitUntilAIEnabled()
    {
        while (CanContinue() && !enemyAIEnabled)
            yield return null;
    }

    private bool CanContinue()
    {
        return initialized &&
               isActiveAndEnabled &&
               hud != null &&
               !hud.BattleEnded &&
               enemy != null &&
               player != null &&
               !enemy.IsDead &&
               !player.IsDead;
    }

    private bool CanAct()
    {
        return enemyAIEnabled &&
               CanContinue() &&
               !enemy.IsDead &&
               enemy.CurrentState !=
               FighterCombatState.Stunned &&
               enemy.CurrentState !=
               FighterCombatState.Dead;
    }

    private bool TryUseConfiguredPermutation()
    {
        if (!enemy.Rules.AiPermutationEnabled ||
            spatialController == null ||
            enemy.Stats.CurrentStamina + Mathf.Epsilon <
                enemy.Rules.ResolvePermutationStaminaCost())
        {
            return false;
        }

        CombatSpatialSnapshot snapshot =
            spatialController.Snapshot;
        if (snapshot.Orientation != RelativeOrientation.Back ||
            snapshot.AdvantageFighter != player)
        {
            return false;
        }

        if (nextPermutationToken < long.MaxValue)
            nextPermutationToken++;
        if (nextPermutationToken <= 0)
            nextPermutationToken = 1;

        CombatActionResult result =
            enemy.TryPermutation(nextPermutationToken);
        if (result != CombatActionResult.Started)
            return false;

        hud.SetEnemyStatus("Permutation");
        hud.ShowMessage(
            "Permutation ennemie",
            new Color(0.78f, 0.66f, 0.25f),
            enemy.Rules.PermutationFeedbackDuration
        );
        return true;
    }

    private void SubscribeFrameClock()
    {
        if (frameTickSubscribed || frameClock == null)
            return;

        frameClock.OnCombatTick += HandleFrameTick;
        frameTickSubscribed = true;
    }

    private void UnsubscribeFrameClock()
    {
        if (!frameTickSubscribed || frameClock == null)
            return;

        frameClock.OnCombatTick -= HandleFrameTick;
        frameTickSubscribed = false;
    }

    private void HandleFrameTick(int globalFrame)
    {
        if (!enemyAIEnabled || !CanContinue())
            return;

        if (AdvanceFrameAttackTelegraph(globalFrame))
            return;

        if (TryRunFrameCompensation(globalFrame))
            return;

        if (enemy.Stats.CurrentStamina + Mathf.Epsilon <
            enemy.LightAttackStaminaCost)
        {
            if (!enemy.IsCharging && !enemy.IsBusy)
                enemy.StartCharge();
            hud.SetEnemyStatus("Recharge");
            return;
        }

        if (enemy.IsCharging)
        {
            enemy.StopChargeInput();
            ScheduleNextDecision(globalFrame);
        }

        if (enemy.IsBusy || globalFrame < nextDecisionFrame)
            return;

        if (TryUseConfiguredPermutation())
        {
            ScheduleNextDecision(globalFrame);
            return;
        }

        BeginFrameAttackTelegraph(
            SelectAffordableAttack(),
            globalFrame
        );
    }

    private void BeginFrameAttackTelegraph(
        CombatActionId attack,
        int globalFrame)
    {
        if (!CombatActionRunner.IsAttack(attack) ||
            frameClock == null)
        {
            ScheduleNextDecision(globalFrame);
            return;
        }

        compensationDueFrame = -1;
        int durationFrames = AttackTelegraphDurationFrames;
        telegraphedAttack = attack;
        telegraphDueFrame = globalFrame + durationFrames;
        string label = AttackLabel(attack);
        hud.SetEnemyStatus($"Preparation {label}");
        hud.ShowMessage(
            $"Attaque {label} imminente",
            TelegraphColor(attack),
            telegraphDuration
        );
    }

    private bool AdvanceFrameAttackTelegraph(int globalFrame)
    {
        if (!IsAttackTelegraphing)
            return false;

        if (enemy.IsBusy)
        {
            CancelFrameAttackTelegraph();
            ScheduleNextDecision(globalFrame);
            return true;
        }

        if (globalFrame < telegraphDueFrame)
            return true;

        CombatActionId attack = telegraphedAttack;
        CancelFrameAttackTelegraph(false);
        CombatActionResult result = ExecuteAttack(attack);
        if (result == CombatActionResult.Started)
        {
            string label = AttackLabel(attack);
            hud.SetEnemyStatus($"Attaque {label}");
            hud.ShowMessage(
                $"Attaque {label}",
                TelegraphColor(attack),
                0.35f
            );
        }
        else if (result == CombatActionResult.NotEnoughStamina)
        {
            enemy.StartCharge();
            hud.SetEnemyStatus("Recharge");
        }

        ScheduleNextDecision(globalFrame);
        return true;
    }

    private void CancelFrameAttackTelegraph(bool clearStatus = true)
    {
        telegraphedAttack = CombatActionId.None;
        telegraphDueFrame = -1;
        RestoreScale();
        if (clearStatus && hud != null)
            hud.SetEnemyStatus(string.Empty);
    }

    private CombatActionId SelectAffordableAttack()
    {
        int roll = UnityEngine.Random.Range(0, 100);
        CombatActionId selected = roll switch
        {
            < 55 => CombatActionId.AttackA,
            < 85 => CombatActionId.AttackB,
            _ => CombatActionId.AttackC
        };

        if (CanAffordAttack(selected))
            return selected;
        if (CanAffordAttack(CombatActionId.AttackB))
            return CombatActionId.AttackB;
        return CombatActionId.AttackA;
    }

    private bool CanAffordAttack(CombatActionId attack)
    {
        CombatActionDefinition definition =
            enemy.FrameRunner?.GetDefinition(attack);
        float cost = definition != null
            ? definition.StaminaCost
            : enemy.LightAttackStaminaCost;
        return enemy.Stats.CurrentStamina + Mathf.Epsilon >= cost;
    }

    private CombatActionResult ExecuteAttack(CombatActionId attack)
    {
        return attack switch
        {
            CombatActionId.AttackB => enemy.MediumAttack(),
            CombatActionId.AttackC => enemy.HeavyAttack(),
            _ => enemy.LightAttack()
        };
    }

    private static string AttackLabel(CombatActionId attack)
    {
        return attack switch
        {
            CombatActionId.AttackB => "B",
            CombatActionId.AttackC => "C",
            _ => "A"
        };
    }

    private static Color TelegraphColor(CombatActionId attack)
    {
        return attack switch
        {
            CombatActionId.AttackB =>
                new Color(1f, 0.42f, 0.18f),
            CombatActionId.AttackC =>
                new Color(0.95f, 0.2f, 0.35f),
            _ => new Color(1f, 0.62f, 0.2f)
        };
    }

    private bool TryRunFrameCompensation(int globalFrame)
    {
        if (compensationDueFrame < 0 ||
            globalFrame < compensationDueFrame)
        {
            return false;
        }

        compensationDueFrame = -1;
        CombatSpatialSnapshot snapshot =
            spatialController != null
                ? spatialController.Snapshot
                : default;
        bool stillCurrent =
            spatialController != null &&
            snapshot.Epoch == compensationEpoch &&
            snapshot.Revision == compensationRevision &&
            snapshot.Orientation == compensationOrientation &&
            snapshot.AdvantageFighter == compensationAdvantage;
        if (!stillCurrent || enemy.IsBusy)
            return false;

        CombatActionResult result =
            compensationDirection == DodgeDirection.Left
                ? enemy.DodgeLeft()
                : enemy.DodgeRight();
        if (result != CombatActionResult.Started)
            return false;

        hud.SetEnemyStatus("Esquive");
        return true;
    }

    private void ScheduleNextDecision(int currentFrame)
    {
        if (frameClock == null)
            return;

        int minimumFrames = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Min(minimumDelay, maximumDelay) *
                frameClock.FramesPerSecond
            )
        );
        int maximumFrames = Mathf.Max(
            minimumFrames,
            Mathf.RoundToInt(
                Mathf.Max(minimumDelay, maximumDelay) *
                frameClock.FramesPerSecond
            )
        );
        nextDecisionFrame =
            currentFrame +
            UnityEngine.Random.Range(
                minimumFrames,
                maximumFrames + 1
            );
    }

    private void UpdateFrameDrivenVisuals()
    {
        if (enemy == null)
            return;

        if (IsAttackTelegraphing)
        {
            float wave =
                0.5f +
                0.5f *
                Mathf.Sin(
                    Time.unscaledTime * telegraphPulseSpeed
                );
            float strength = wave * telegraphScaleStrength;
            transform.localScale = Vector3.Scale(
                normalScale,
                new Vector3(
                    1f - strength * 0.35f,
                    1f + strength,
                    1f - strength * 0.35f
                )
            );
            return;
        }

        if (enemy.CurrentState ==
            FighterCombatState.AttackStartup)
        {
            float pulse =
                1f +
                Mathf.Sin(Time.time * pulseSpeed) *
                pulseStrength;
            transform.localScale = normalScale * pulse;
            return;
        }

        RestoreScale();
        if (hud != null &&
            !enemy.IsBusy &&
            !enemy.IsCharging)
        {
            hud.SetEnemyStatus(string.Empty);
        }
    }

    private void ApplyAIEnabledState()
    {
        bool changed =
            appliedAIEnabled != enemyAIEnabled;
        appliedAIEnabled = enemyAIEnabled;

        if (enemyAIEnabled)
            StartAI();
        else
            StopAI();

        if (changed)
            OnAIEnabledChanged?.Invoke(enemyAIEnabled);
    }

    private void RestoreScale()
    {
        if (normalScale != Vector3.zero)
            transform.localScale = normalScale;
    }

    private void SubscribeSpatialEvents()
    {
        if (spatialController != null)
        {
            spatialController.OnDodgeCommitted +=
                HandleDodgeCommitted;
        }
    }

    private void UnsubscribeSpatialEvents()
    {
        if (spatialController != null)
        {
            spatialController.OnDodgeCommitted -=
                HandleDodgeCommitted;
        }
    }

    private void HandleDodgeCommitted(
        SpatialDodgeTransaction transaction)
    {
        if (!CanAct() ||
            spatialController == null ||
            transaction.Fighter != player ||
            transaction.Direction is not DodgeDirection.Left and
                not DodgeDirection.Right ||
            IsAttackTelegraphing ||
            !enemy.Rules.AiCompensationEnabled)
        {
            return;
        }

        CombatSpatialSnapshot snapshot =
            spatialController.Snapshot;
        bool playerHasFlank =
            snapshot.AdvantageFighter == player &&
            (snapshot.Orientation ==
                RelativeOrientation.LeftFlank ||
             snapshot.Orientation ==
                RelativeOrientation.RightFlank);
        if (!playerHasFlank ||
            UnityEngine.Random.value >
                enemy.Rules.AiCompensationProbability)
        {
            return;
        }

        if (frameClock != null)
        {
            compensationDirection = transaction.Direction;
            compensationEpoch = snapshot.Epoch;
            compensationRevision = snapshot.Revision;
            compensationOrientation = snapshot.Orientation;
            compensationAdvantage = snapshot.AdvantageFighter;
            int minimumFrames = Mathf.RoundToInt(
                enemy.Rules.AiCompensationMinDelay *
                frameClock.FramesPerSecond
            );
            int maximumFrames = Mathf.RoundToInt(
                enemy.Rules.AiCompensationMaxDelay *
                frameClock.FramesPerSecond
            );
            compensationDueFrame =
                frameClock.CurrentFrame +
                UnityEngine.Random.Range(
                    Mathf.Min(minimumFrames, maximumFrames),
                    Mathf.Max(minimumFrames, maximumFrames) + 1
                );
            return;
        }

        if (compensationRoutine != null)
            StopCoroutine(compensationRoutine);

        compensationRoutine = StartCoroutine(
            CompensationRoutine(
                transaction.Direction,
                snapshot.Epoch,
                snapshot.Revision,
                snapshot.Orientation,
                snapshot.AdvantageFighter
            )
        );
    }

    private IEnumerator CompensationRoutine(
        DodgeDirection direction,
        long expectedEpoch,
        int expectedRevision,
        RelativeOrientation expectedOrientation,
        FighterCombat expectedAdvantage)
    {
        int generation = routineGeneration;
        float delay = UnityEngine.Random.Range(
            enemy.Rules.AiCompensationMinDelay,
            enemy.Rules.AiCompensationMaxDelay
        );
        float elapsed = 0f;

        while (elapsed < delay &&
               generation == routineGeneration &&
               CanAct())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        CombatSpatialSnapshot currentSnapshot =
            spatialController != null
                ? spatialController.Snapshot
                : default;
        bool spatialStateIsStillCurrent =
            spatialController != null &&
            currentSnapshot.Epoch == expectedEpoch &&
            currentSnapshot.Revision == expectedRevision &&
            currentSnapshot.Orientation ==
                expectedOrientation &&
            currentSnapshot.AdvantageFighter ==
                expectedAdvantage;
        if (generation == routineGeneration &&
            CanAct() &&
            spatialStateIsStillCurrent)
        {
            CombatActionResult result =
                direction == DodgeDirection.Left
                    ? enemy.DodgeLeft()
                    : enemy.DodgeRight();
            if (result == CombatActionResult.Started)
                hud.SetEnemyStatus("Esquive");
        }

        if (generation == routineGeneration)
            compensationRoutine = null;
    }
}
