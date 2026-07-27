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

        if (playerStats == null || enemyStats == null)
        {
            Debug.LogError(
                "CombatManager requires FighterStats on both fighters."
            );
            enabled = false;
            return;
        }

        hud = CombatHUD.Create(
            playerCombat,
            enemyCombat,
            RestartCombat
        );

        enemyAI = enemyCombat.GetComponent<EnemyAutoCombat>();
        if (enemyAI == null)
        {
            enemyAI =
                enemyCombat.gameObject.AddComponent<
                    EnemyAutoCombat>();
        }

        prototypeDebugUI = PrototypeDebugUI.Create(
            hud.transform,
            enemyAI,
            hud.GestureGrid
        );

        feedbackEffects =
            gameObject.AddComponent<CombatFeedbackEffects>();
        feedbackEffects.Initialize(
            playerCombat,
            enemyCombat,
            Camera.main
        );

        Subscribe();
        StartCombat();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (enemyAI != null)
            enemyAI.StopAI();

        if (feedbackEffects != null)
            feedbackEffects.ResetEffects();
    }

    public void RestartCombat()
    {
        if (isResetting)
            return;

        isResetting = true;

        enemyAI?.StopAI();
        feedbackEffects?.ResetEffects();

        playerCombat.SetCombatEnabled(false);
        enemyCombat.SetCombatEnabled(false);

        playerStats.ResetStats();
        enemyStats.ResetStats();

        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();

        combatEnded = false;
        hud.HideEndState();
        hud.RefreshAll();
        hud.SetGridEnabled(true);

        enemyAI.Initialize(enemyCombat, playerCombat, hud);
        prototypeDebugUI?.ResetForReplay();
        isResetting = false;
    }

    private void StartCombat()
    {
        combatEnded = false;
        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();
        hud.HideEndState();
        hud.SetGridEnabled(true);
        hud.RefreshAll();
        enemyAI.Initialize(enemyCombat, playerCombat, hud);
        prototypeDebugUI?.ResetForReplay();
    }

    private void EndCombat(bool playerWon)
    {
        if (combatEnded || isResetting)
            return;

        combatEnded = true;
        enemyAI?.StopAI();

        playerCombat.SetCombatEnabled(false);
        enemyCombat.SetCombatEnabled(false);
        hud.ShowEndState(playerWon);
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
}
