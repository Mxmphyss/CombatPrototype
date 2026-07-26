using System.Collections;
using UnityEngine;

public sealed class EnemyAutoCombat : MonoBehaviour
{
    [Header("Rythme")]
    [SerializeField] private float minimumDelay = 1.5f;
    [SerializeField] private float maximumDelay = 2.5f;
    [SerializeField] private float telegraphDuration = 0.6f;

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
        Unsubscribe();

        enemy = enemyCombat;
        player = playerCombat;
        hud = combatHud;
        normalScale = transform.localScale;
        initialized = true;

        Subscribe();
        StartAI();
    }

    private void OnDisable()
    {
        StopAI();
        Unsubscribe();
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

        transform.localScale = normalScale == Vector3.zero
            ? transform.localScale
            : normalScale;

        if (hud != null && !hud.BattleEnded)
            hud.SetEnemyStatus(string.Empty);
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

            while (enemy.IsBusy && CanContinue())
                yield return null;

            if (!CanContinue())
                break;

            hud.SetEnemyStatus("Attaque");
            hud.ShowMessage(
                "Attaque ennemie",
                new Color(0.94f, 0.42f, 0.30f),
                telegraphDuration
            );
            yield return TelegraphAttack();

            if (!CanContinue())
                break;

            CombatActionResult result = enemy.LightAttack();
            if (result == CombatActionResult.Started)
            {
                while (enemy.IsBusy && CanContinue())
                    yield return null;
            }
            else if (
                result ==
                CombatActionResult.NotEnoughStamina)
            {
                yield return RechargeUntilAttackIsAvailable();
            }

            if (CanContinue())
                hud.SetEnemyStatus(string.Empty);
        }

        transform.localScale = normalScale;
        combatRoutine = null;
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
                CombatActionResult result =
                    enemy.StartCharge();
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

    private IEnumerator TelegraphAttack()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, telegraphDuration);

        while (elapsed < duration && CanContinue())
        {
            elapsed += Time.deltaTime;
            float pulse =
                1f + Mathf.Sin(elapsed * 18f) * 0.025f;
            transform.localScale = normalScale * pulse;
            yield return null;
        }

        transform.localScale = normalScale;
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

    private void Subscribe()
    {
        if (enemy?.Stats != null)
            enemy.Stats.OnDeath += HandleDeath;

        if (player?.Stats != null)
            player.Stats.OnDeath += HandleDeath;
    }

    private void Unsubscribe()
    {
        if (enemy?.Stats != null)
            enemy.Stats.OnDeath -= HandleDeath;

        if (player?.Stats != null)
            player.Stats.OnDeath -= HandleDeath;
    }

    private void HandleDeath(FighterStats deadStats)
    {
        StopAI();
    }
}
