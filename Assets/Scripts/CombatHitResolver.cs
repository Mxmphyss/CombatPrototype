using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatResolvedImpact
{
    public CombatImpactCandidate Candidate { get; }
    public CombatFrameOutcome Outcome { get; }
    public RelativeOrientation Orientation { get; }
    public float Damage { get; }
    public float PositionalMultiplier { get; }

    public CombatResolvedImpact(
        CombatImpactCandidate candidate,
        CombatFrameOutcome outcome,
        RelativeOrientation orientation,
        float damage,
        float positionalMultiplier)
    {
        Candidate = candidate;
        Outcome = outcome;
        Orientation = orientation;
        Damage = Mathf.Max(0f, damage);
        PositionalMultiplier = Mathf.Max(0f, positionalMultiplier);
    }
}

public sealed class CombatHitResolver
{
    private readonly List<CombatResolvedImpact> resolved = new(2);
    private CombatSpatialController spatial;

    public void Initialize(CombatSpatialController spatialAuthority)
    {
        spatial = spatialAuthority;
    }

    public IReadOnlyList<CombatResolvedImpact> Resolve(
        IReadOnlyList<CombatImpactCandidate> candidates)
    {
        resolved.Clear();
        if (candidates == null)
            return resolved;

        for (int i = 0; i < candidates.Count; i++)
        {
            CombatImpactCandidate candidate = candidates[i];
            if (TryResolveCandidate(
                    candidate,
                    out CombatResolvedImpact impact))
            {
                resolved.Add(impact);
            }
        }

        if (resolved.Count == 2 &&
            IsDamagingOutcome(resolved[0].Outcome) &&
            IsDamagingOutcome(resolved[1].Outcome) &&
            resolved[0].Candidate.Attacker ==
                resolved[1].Candidate.Target &&
            resolved[1].Candidate.Attacker ==
                resolved[0].Candidate.Target)
        {
            resolved[0] = WithOutcome(
                resolved[0],
                CombatFrameOutcome.Trade
            );
            resolved[1] = WithOutcome(
                resolved[1],
                CombatFrameOutcome.Trade
            );
        }

        return resolved;
    }

    public void Apply(
        IReadOnlyList<CombatResolvedImpact> impacts)
    {
        if (impacts == null)
            return;

        // All outcomes were classified before this loop. Therefore a hit
        // cannot erase the opponent's valid same-tick impact.
        for (int i = 0; i < impacts.Count; i++)
        {
            CombatResolvedImpact impact = impacts[i];
            impact.Candidate.Attacker.MarkAttackResolved();
        }

        for (int i = 0; i < impacts.Count; i++)
            ApplyOne(impacts[i]);
    }

    private bool TryResolveCandidate(
        CombatImpactCandidate candidate,
        out CombatResolvedImpact impact)
    {
        impact = default;
        CombatActionRunner attacker = candidate.Attacker;
        CombatActionRunner target = candidate.Target;
        if (attacker == null ||
            target == null ||
            attacker.IsDead ||
            target.IsDead)
        {
            return false;
        }

        Vector3 difference = target.Owner.transform.position -
                             attacker.Owner.transform.position;
        float distance = Vector3.ProjectOnPlane(
            difference,
            Vector3.up
        ).magnitude;
        ResolvedCombatAction action = candidate.Action;
        if (distance + 0.0001f < action.MinimumRange ||
            distance - 0.0001f > action.MaximumRange)
        {
            return false;
        }

        bool insideArc = spatial != null
            ? spatial.IsTargetInsideAttackArc(
                attacker.Owner,
                target.Owner,
                action.AttackConeDegrees
            )
            : attacker.Owner.CanHitCurrentTarget();
        if (!insideArc)
            return false;

        RelativeOrientation orientation = spatial != null
            ? spatial.GetAttackOrientation(
                attacker.Owner,
                target.Owner
            )
            : RelativeOrientation.Face;
        float multiplier = spatial != null
            ? spatial.GetDamageMultiplier(
                attacker.Owner,
                target.Owner
            )
            : 1f;
        float damage = action.Damage * multiplier;

        CombatFrameOutcome outcome;
        if (action.BaseDefinition.Dodgeable &&
            target.IsInvulnerable)
        {
            outcome = target.IsPerfectDodgeWindow
                ? CombatFrameOutcome.PerfectDodge
                : CombatFrameOutcome.Dodge;
        }
        else if (action.BaseDefinition.Parryable &&
                 target.IsParryActive &&
                 orientation == RelativeOrientation.Face)
        {
            outcome = CombatFrameOutcome.Parry;
        }
        else if (action.BaseDefinition.Blockable &&
                 target.IsGuarding &&
                 orientation == RelativeOrientation.Face)
        {
            float staminaAfter =
                target.Stats.CurrentStamina -
                action.GuardStaminaDamage;
            outcome = staminaAfter <= Mathf.Epsilon
                ? CombatFrameOutcome.GuardBreak
                : CombatFrameOutcome.Block;
        }
        else
        {
            outcome = ClassifyDamagingHit(target);
        }

        impact = new CombatResolvedImpact(
            candidate,
            outcome,
            orientation,
            damage,
            multiplier
        );
        return true;
    }

    private void ApplyOne(CombatResolvedImpact impact)
    {
        CombatActionRunner attacker = impact.Candidate.Attacker;
        CombatActionRunner target = impact.Candidate.Target;
        ResolvedCombatAction action = impact.Candidate.Action;

        switch (impact.Outcome)
        {
            case CombatFrameOutcome.Dodge:
                target.SetOutcome(CombatFrameOutcome.Dodge);
                break;

            case CombatFrameOutcome.PerfectDodge:
                target.SetOutcome(CombatFrameOutcome.PerfectDodge);
                attacker.ApplyPerfectDodgeCounter();
                break;

            case CombatFrameOutcome.Parry:
                target.CompleteParry();
                break;

            case CombatFrameOutcome.Block:
                float blockedStamina =
                    target.Stats.ApplyStaminaDamage(
                    action.GuardStaminaDamage
                );
                target.Owner.FrameRaiseGuardImpact(
                    new GuardImpact(
                        target.Owner,
                        blockedStamina,
                        false,
                        impact.Candidate.GlobalFrame /
                        (float)CombatFrameClock.DefaultFramesPerSecond
                    )
                );
                target.ApplyBlockstun(action.BlockstunFrames);
                target.SetOutcome(CombatFrameOutcome.Block);
                break;

            case CombatFrameOutcome.GuardBreak:
                float brokenStamina =
                    target.Stats.ApplyStaminaDamage(
                    action.GuardStaminaDamage
                );
                target.Owner.FrameRaiseGuardImpact(
                    new GuardImpact(
                        target.Owner,
                        brokenStamina,
                        true,
                        impact.Candidate.GlobalFrame /
                        (float)CombatFrameClock.DefaultFramesPerSecond
                    )
                );
                target.ApplyGuardBreak();
                target.SetOutcome(CombatFrameOutcome.GuardBreak);
                break;

            case CombatFrameOutcome.Hit:
            case CombatFrameOutcome.CounterHit:
            case CombatFrameOutcome.Punish:
            case CombatFrameOutcome.Trade:
                target.Stats.TakeDamage(impact.Damage);
                int hitstun = action.HitstunFrames;
                if (impact.Outcome ==
                    CombatFrameOutcome.CounterHit)
                {
                    hitstun += action.CounterHitBonusFrames;
                }
                target.ApplyHitstun(
                    hitstun,
                    impact.Outcome
                );
                break;
        }

        if (impact.Outcome != CombatFrameOutcome.Dodge &&
            impact.Outcome != CombatFrameOutcome.PerfectDodge)
        {
            attacker.SetHitstop(action.HitstopFrames);
            target.SetHitstop(action.HitstopFrames);
        }

        attacker.SetOutcome(impact.Outcome);
        attacker.Owner.FrameRaiseImpact(
            new CombatImpact(
                attacker.Owner,
                target.Owner,
                ToLegacyHitResult(impact.Outcome),
                impact.Candidate.GlobalFrame /
                (float)CombatFrameClock.DefaultFramesPerSecond,
                impact.Orientation,
                impact.PositionalMultiplier,
                IsDamagingOutcome(impact.Outcome)
                    ? impact.Damage
                    : 0f
            )
        );
    }

    private static CombatFrameOutcome ClassifyDamagingHit(
        CombatActionRunner target)
    {
        if (target.CurrentPhase is
            CombatActionPhase.Startup or
            CombatActionPhase.Active)
        {
            return CombatFrameOutcome.CounterHit;
        }

        if (target.CurrentPhase ==
            CombatActionPhase.Recovery)
        {
            return CombatFrameOutcome.Punish;
        }

        return CombatFrameOutcome.Hit;
    }

    private static bool IsDamagingOutcome(
        CombatFrameOutcome outcome)
    {
        return outcome is
            CombatFrameOutcome.Hit or
            CombatFrameOutcome.CounterHit or
            CombatFrameOutcome.Punish or
            CombatFrameOutcome.Trade;
    }

    private static CombatResolvedImpact WithOutcome(
        CombatResolvedImpact impact,
        CombatFrameOutcome outcome)
    {
        return new CombatResolvedImpact(
            impact.Candidate,
            outcome,
            impact.Orientation,
            impact.Damage,
            impact.PositionalMultiplier
        );
    }

    private static CombatHitResult ToLegacyHitResult(
        CombatFrameOutcome outcome)
    {
        return outcome switch
        {
            CombatFrameOutcome.Block =>
                CombatHitResult.Blocked,
            CombatFrameOutcome.GuardBreak =>
                CombatHitResult.GuardBroken,
            CombatFrameOutcome.Parry =>
                CombatHitResult.PerfectGuard,
            CombatFrameOutcome.Dodge =>
                CombatHitResult.Dodged,
            CombatFrameOutcome.PerfectDodge =>
                CombatHitResult.PerfectDodge,
            CombatFrameOutcome.Whiff =>
                CombatHitResult.Missed,
            CombatFrameOutcome.InterruptedDodge =>
                CombatHitResult.Interrupted,
            _ => CombatHitResult.Hit
        };
    }
}
