using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatFrameSystem : MonoBehaviour
{
    [SerializeField]
    private CombatFrameDataSettings frameData = new();

    private readonly List<CombatImpactCandidate> candidates = new(2);
    private CombatFrameClock clock;
    private CombatActionRunner playerRunner;
    private CombatActionRunner enemyRunner;
    private CombatHitResolver hitResolver;
    private CombatSpatialController spatial;
    private bool initialized;
    private bool combatEnabled;

    public event Action<int> OnFrameCompleted;
    public event Action<CombatActionRunner, CombatFrameOutcome>
        OnOutcome;

    public CombatFrameClock Clock => clock;
    public CombatFrameDataSettings Settings => frameData;
    public CombatActionRunner PlayerRunner => playerRunner;
    public CombatActionRunner EnemyRunner => enemyRunner;
    public int CurrentFrame => clock != null ? clock.CurrentFrame : 0;

    public void Initialize(
        CombatFrameClock frameClock,
        FighterCombat player,
        FighterCombat enemy,
        CombatSpatialController spatialAuthority,
        CombatRulesConfig rules)
    {
        if (initialized)
            Shutdown();

        clock = frameClock;
        spatial = spatialAuthority;
        hitResolver = new CombatHitResolver();
        hitResolver.Initialize(spatial);
        playerRunner = new CombatActionRunner(frameData);
        enemyRunner = new CombatActionRunner(frameData);
        playerRunner.Initialize(this, player, enemy, spatial, rules);
        enemyRunner.Initialize(this, enemy, player, spatial, rules);
        player.FrameAttachRunner(playerRunner);
        enemy.FrameAttachRunner(enemyRunner);

        spatial?.SetFrameDriven(
            true,
            frameData.FramesPerSecond,
            frameData.FlankAutoFaceFrames
        );

        clock.Configure(frameData);
        clock.OnCombatTick += HandleCombatTick;
        clock.ResetClock(false);
        combatEnabled = true;
        initialized = true;
        clock.StartClock();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    public CombatActionRunner GetRunner(FighterCombat fighter)
    {
        if (fighter == playerRunner?.Owner)
            return playerRunner;
        if (fighter == enemyRunner?.Owner)
            return enemyRunner;
        return null;
    }

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;
        playerRunner?.SetCombatEnabled(enabled);
        enemyRunner?.SetCombatEnabled(enabled);
        if (enabled)
            clock?.StartClock();
        else
            clock?.StopClock();
    }

    public void ResetSystem()
    {
        if (!initialized)
            return;

        clock.ResetClock(false);
        candidates.Clear();
        playerRunner.Reset(true);
        enemyRunner.Reset(true);
        spatial?.CancelTransientState();
        combatEnabled = true;
        clock.StartClock();
    }

    public void NotifyOutcome(
        CombatActionRunner runner,
        CombatFrameOutcome outcome)
    {
        OnOutcome?.Invoke(runner, outcome);
    }

    public void AdvanceFramesForTests(int count)
    {
        clock?.AdvanceFramesForTests(count);
    }

    private void HandleCombatTick(int globalFrame)
    {
        if (!initialized || !combatEnabled)
            return;

        playerRunner.BeginTick(globalFrame);
        enemyRunner.BeginTick(globalFrame);

        spatial?.TickFrame();

        candidates.Clear();
        if (playerRunner.TryCreateImpactCandidate(
                globalFrame,
                out CombatImpactCandidate playerImpact))
        {
            candidates.Add(playerImpact);
        }
        if (enemyRunner.TryCreateImpactCandidate(
                globalFrame,
                out CombatImpactCandidate enemyImpact))
        {
            candidates.Add(enemyImpact);
        }

        IReadOnlyList<CombatResolvedImpact> impacts =
            hitResolver.Resolve(candidates);
        hitResolver.Apply(impacts);

        playerRunner.EndTick();
        enemyRunner.EndTick();
        OnFrameCompleted?.Invoke(globalFrame);
    }

    private void Shutdown()
    {
        if (clock != null)
            clock.OnCombatTick -= HandleCombatTick;
        playerRunner?.CancelAll(false);
        enemyRunner?.CancelAll(false);
        spatial?.SetFrameDriven(false, 60, 180);
        initialized = false;
    }
}
