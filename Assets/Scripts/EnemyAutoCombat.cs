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

    private FighterCombat enemy;
    private FighterCombat player;
    private CombatHUD hud;
    private CombatSpatialController spatialController;
    private Coroutine combatRoutine;
    private Coroutine compensationRoutine;
    private Vector3 normalScale;
    private bool initialized;
    private bool appliedAIEnabled = true;
    private int routineGeneration;
    private long nextPermutationToken;

    public event Action<bool> OnAIEnabledChanged;

    public bool EnemyAIEnabled => enemyAIEnabled;

    public void Initialize(
        FighterCombat enemyCombat,
        FighterCombat playerCombat,
        CombatHUD combatHud,
        CombatSpatialController spatialAuthority = null)
    {
        UnsubscribeSpatialEvents();
        StopAI();

        enemy = enemyCombat;
        player = playerCombat;
        hud = combatHud;
        spatialController = spatialAuthority;
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
