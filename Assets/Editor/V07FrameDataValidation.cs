using System;
using UnityEditor;
using UnityEngine;

public static class V07FrameDataValidation
{
    public static void Run()
    {
        try
        {
            ValidateActionProfiles();
            ValidateDodgeWindows();
            ValidateCommandBuffer();
            ValidateClock();
            Debug.Log(
                "V07FrameDataValidation: profiles, advantage, " +
                "dodge windows, command buffer and 60 Hz clock passed."
            );
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "V07FrameDataValidation failed: " + exception
            );
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateActionProfiles()
    {
        CombatFrameDataSettings settings = new();
        CombatActionDefinition attackA = settings.CreateAttackA();
        CombatActionDefinition attackB = settings.CreateAttackB();
        CombatActionDefinition attackC = settings.CreateAttackC();

        ValidateAttack(
            attackA,
            CombatActionId.AttackA,
            7,
            3,
            12,
            3,
            16,
            9,
            3,
            2,
            -5
        );
        ValidateAttack(
            attackB,
            CombatActionId.AttackB,
            11,
            4,
            17,
            4,
            22,
            13,
            3,
            2,
            -7
        );
        ValidateAttack(
            attackC,
            CombatActionId.AttackC,
            18,
            5,
            26,
            6,
            34,
            18,
            4,
            4,
            -12
        );

        Require(
            attackA.Damage < attackB.Damage &&
            attackB.Damage < attackC.Damage,
            "Attack damage hierarchy A < B < C is invalid."
        );
        Require(
            attackA.StaminaCost < attackB.StaminaCost &&
            attackB.StaminaCost < attackC.StaminaCost,
            "Attack stamina hierarchy A < B < C is invalid."
        );
        Require(
            attackA.MaximumRange < attackB.MaximumRange &&
            attackB.MaximumRange < attackC.MaximumRange,
            "Attack range hierarchy A < B < C is invalid."
        );
        RequireEqual(settings.FramesPerSecond, 60, "Clock rate");
        RequireEqual(settings.InputBufferFrames, 6, "Buffer length");
        RequireEqual(settings.GuardBreakFrames, 240, "Guard break");
        RequireEqual(
            settings.PermutationStartupFrames,
            3,
            "Permutation startup"
        );
        RequireEqual(
            settings.PermutationActiveFrames,
            5,
            "Permutation active"
        );
        RequireEqual(
            settings.PermutationRecoveryFrames,
            6,
            "Permutation recovery"
        );
        RequireEqual(settings.RiposteWindowFrames, 30, "Riposte");
        RequireEqual(settings.FlankAutoFaceFrames, 180, "Flank timer");
    }

    private static void ValidateAttack(
        CombatActionDefinition attack,
        CombatActionId id,
        int startup,
        int active,
        int recovery,
        int hitstop,
        int hitstun,
        int blockstun,
        int counterBonus,
        int hitAdvantage,
        int blockAdvantage)
    {
        RequireEqual(attack.Id, id, $"{id} id");
        RequireEqual(attack.StartupFrames, startup, $"{id} startup");
        RequireEqual(attack.ActiveFrames, active, $"{id} active");
        RequireEqual(attack.RecoveryFrames, recovery, $"{id} recovery");
        RequireEqual(
            attack.TotalFrames,
            startup + active + recovery,
            $"{id} total"
        );
        RequireEqual(attack.HitstopFrames, hitstop, $"{id} hitstop");
        RequireEqual(attack.HitstunFrames, hitstun, $"{id} hitstun");
        RequireEqual(
            attack.BlockstunFrames,
            blockstun,
            $"{id} blockstun"
        );
        RequireEqual(
            attack.CounterHitBonusFrames,
            counterBonus,
            $"{id} counter bonus"
        );
        RequireEqual(
            attack.AdvantageOnHitFirstActive,
            hitAdvantage,
            $"{id} first-active hit advantage"
        );
        RequireEqual(
            attack.AdvantageOnBlockFirstActive,
            blockAdvantage,
            $"{id} first-active block advantage"
        );
        RequireNear(
            attack.AttackConeDegrees,
            100f,
            $"{id} attack cone"
        );
    }

    private static void ValidateDodgeWindows()
    {
        CombatFrameDataSettings settings = new();
        RequireEqual(settings.DodgeTotalFrames, 26, "Dodge total");
        RequireWindow(
            settings.DodgeInvulnerabilityWindow,
            5,
            19,
            "Dodge invulnerability"
        );
        RequireWindow(
            settings.PerfectDodgeWindow,
            9,
            15,
            "Perfect dodge"
        );
        Require(
            !settings.DodgeInvulnerabilityWindow.Contains(4) &&
            settings.DodgeInvulnerabilityWindow.Contains(5) &&
            settings.DodgeInvulnerabilityWindow.Contains(18) &&
            !settings.DodgeInvulnerabilityWindow.Contains(19),
            "Dodge half-open interval is incorrect."
        );
    }

    private static void ValidateCommandBuffer()
    {
        CombatCommandBuffer buffer = new(6);
        CombatFrameCommand attackA =
            new(CombatActionId.AttackA, 10);
        CombatFrameCommand attackB =
            new(CombatActionId.AttackB, 12);

        RequireEqual(
            buffer.Store(attackA),
            CombatCommandBufferStatus.Buffered,
            "Initial buffer status"
        );
        RequireEqual(
            buffer.Store(attackB),
            CombatCommandBufferStatus.Replaced,
            "Replacement status"
        );
        RequireEqual(
            buffer.BufferedAction,
            CombatActionId.AttackB,
            "Replacement command"
        );
        RequireEqual(buffer.RemainingFrames(12), 6, "Fresh lifetime");
        Require(
            buffer.TryConsume(17, out CombatFrameCommand consumed) &&
            consumed.ActionId == CombatActionId.AttackB,
            "Buffered command was not consumable before expiry."
        );

        buffer.Store(new CombatFrameCommand(CombatActionId.AttackC, 20));
        Require(buffer.UpdateExpiration(26), "Buffer did not expire.");
        Require(!buffer.HasCommand, "Expired command survived.");
        RequireEqual(
            buffer.LastStatus,
            CombatCommandBufferStatus.Expired,
            "Expiry status"
        );
    }

    private static void ValidateClock()
    {
        GameObject clockObject = new("V07 Validation Clock");
        try
        {
            CombatFrameClock clock =
                clockObject.AddComponent<CombatFrameClock>();
            clock.Configure(new CombatFrameDataSettings());
            int observedTicks = 0;
            int lastFrame = 0;
            clock.OnCombatTick += frame =>
            {
                observedTicks++;
                RequireEqual(
                    frame,
                    lastFrame + 1,
                    "Non-contiguous frame"
                );
                lastFrame = frame;
            };
            clock.AdvanceFramesForTests(60);
            RequireEqual(observedTicks, 60, "Observed ticks");
            RequireEqual(clock.CurrentFrame, 60, "Clock frame");
            RequireNear(clock.SecondsPerFrame, 1f / 60f, "Frame duration");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clockObject);
        }
    }

    private static void RequireWindow(
        CombatFrameWindow window,
        int start,
        int end,
        string label)
    {
        RequireEqual(window.StartInclusive, start, label + " start");
        RequireEqual(window.EndExclusive, end, label + " end");
    }

    private static void RequireNear(
        float actual,
        float expected,
        string label)
    {
        Require(
            Mathf.Abs(actual - expected) <= 0.0001f,
            $"{label}: expected {expected}, got {actual}."
        );
    }

    private static void RequireEqual<T>(
        T actual,
        T expected,
        string label)
    {
        Require(
            Equals(actual, expected),
            $"{label}: expected {expected}, got {actual}."
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
