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
            hud.GestureGrid,
            spatialController
        );

        feedbackEffects =
            gameObject.AddComponent<CombatFeedbackEffects>();
        feedbackEffects.Initialize(
            playerCombat,
            enemyCombat,
            Camera.main,
            spatialController
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

        CancelTransientCombatState(true);

        playerStats.ResetStats();
        enemyStats.ResetStats();

        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();
        spatialController?.ResetDuel();
        spatialController?.SetCombatEnabled(true);

        combatEnded = false;
        hud.HideEndState();
        hud.RefreshAll();
        hud.SetGridEnabled(true);

        enemyAI.Initialize(
            enemyCombat,
            playerCombat,
            hud,
            spatialController
        );
        prototypeDebugUI?.ResetForReplay();
        isResetting = false;
    }

    private void StartCombat()
    {
        combatEnded = false;
        hud.SetGridEnabled(false);
        hud.GestureGrid?.ResetForReplay();
        spatialController?.SetCombatEnabled(false);
        playerCombat.ResetCombatState();
        enemyCombat.ResetCombatState();
        spatialController?.ResetDuel();
        spatialController?.SetCombatEnabled(true);
        hud.HideEndState();
        hud.SetGridEnabled(true);
        hud.RefreshAll();
        enemyAI.Initialize(
            enemyCombat,
            playerCombat,
            hud,
            spatialController
        );
        prototypeDebugUI?.ResetForReplay();
    }

    private void EndCombat(bool playerWon)
    {
        if (combatEnded || isResetting)
            return;

        combatEnded = true;
        CancelTransientCombatState(true);
        hud.ShowEndState(playerWon);
    }

    private void CancelTransientCombatState(
        bool resetFeedback)
    {
        enemyAI?.StopAI();
        hud?.SetGridEnabled(false);
        hud?.GestureGrid?.ResetForReplay();
        if (resetFeedback)
            feedbackEffects?.ResetEffects();

        spatialController?.SetCombatEnabled(false);
        playerCombat?.SetCombatEnabled(false);
        enemyCombat?.SetCombatEnabled(false);
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
