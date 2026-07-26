using System.Collections;
using UnityEngine;

public sealed class EnemyAutoCombat : MonoBehaviour
{
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

        StartAI();
    }

    private void OnDisable()
    {
        StopAI();
    }

    public void StartAI()
    {
        if (!initialized ||
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

    private IEnumerator CombatLoop()
    {
        while (CanContinue())
        {
            float delay = Random.Range(
                Mathf.Min(minimumDelay, maximumDelay),
                Mathf.Max(minimumDelay, maximumDelay)
            );
            yield return WaitWhileCombatContinues(delay);

            if (!CanContinue())
                break;

            while (enemy.IsBusy && CanContinue())
                yield return null;

            if (!CanContinue())
                break;

            if (enemy.Stats.CurrentStamina + Mathf.Epsilon <
                enemy.LightAttackStaminaCost)
            {
                yield return RechargeUntilAttackIsAvailable();
            }

            if (!CanContinue())
                break;

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
            if (CanContinue())
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

        enemy.StopChargeInput();

        if (CanContinue())
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
            elapsed += Time.deltaTime;
            yield return null;
        }
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

    private void RestoreScale()
    {
        if (normalScale != Vector3.zero)
            transform.localScale = normalScale;
    }
}
