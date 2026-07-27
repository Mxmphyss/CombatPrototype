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
    private Coroutine combatRoutine;
    private Vector3 normalScale;
    private bool initialized;
    private bool appliedAIEnabled = true;

    public event Action<bool> OnAIEnabledChanged;

    public bool EnemyAIEnabled => enemyAIEnabled;

    public void Initialize(
        FighterCombat enemyCombat,
        FighterCombat playerCombat,
        CombatHUD combatHud)
    {
        StopAI();

        enemy = enemyCombat;
        player = playerCombat;
        hud = combatHud;
        normalScale = transform.localScale;
        initialized = true;
        appliedAIEnabled = enemyAIEnabled;

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
        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        if (enemy != null)
            enemy.StopChargeInput();

        RestoreScale();
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
        while (CanContinue())
        {
            yield return WaitUntilAIEnabled();

            if (!CanAct())
                continue;

            float delay = UnityEngine.Random.Range(
                Mathf.Min(minimumDelay, maximumDelay),
                Mathf.Max(minimumDelay, maximumDelay)
            );
            yield return WaitWhileCombatContinues(delay);

            if (!CanAct())
                continue;

            while (enemy.IsBusy && CanContinue())
                yield return null;

            if (!CanAct())
                continue;

            if (enemy.Stats.CurrentStamina + Mathf.Epsilon <
                enemy.LightAttackStaminaCost)
            {
                yield return RechargeUntilAttackIsAvailable();
            }

            if (!CanAct())
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
        return enemyAIEnabled && CanContinue();
    }

    private void ApplyAIEnabledState()
    {
        bool changed =
            appliedAIEnabled != enemyAIEnabled;
        appliedAIEnabled = enemyAIEnabled;

        if (enemyAIEnabled)
            StartAI();

        if (changed)
            OnAIEnabledChanged?.Invoke(enemyAIEnabled);
    }

    private void RestoreScale()
    {
        if (normalScale != Vector3.zero)
            transform.localScale = normalScale;
    }
}
