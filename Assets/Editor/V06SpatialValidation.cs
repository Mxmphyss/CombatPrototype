using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class V06SpatialValidation
{
    private const float PositionTolerance = 0.001f;
    private const float ScalarTolerance = 0.001f;
    private const float ExpectedMidDistance = 6f;
    private const float ExpectedCloseDistance = 3f;
    private const float ExpectedLongDistance = 9f;
    private const float ExpectedStrafeSpeed = 1.5f;
    private const float ExpectedFlankMultiplier = 1.25f;
    private const float ExpectedBackMultiplier = 2f;
    private const float ExpectedPermutationCost = 50f;

    private static readonly MethodInfo UpdateContinuousMovementMethod =
        typeof(CombatSpatialController).GetMethod(
            "UpdateContinuousMovement",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly MethodInfo UpdateAutoFaceMethod =
        typeof(CombatSpatialController).GetMethod(
            "UpdateAutoFace",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly MethodInfo MoveAttackTowardTargetMethod =
        typeof(FighterCombat).GetMethod(
            "MoveAttackTowardTarget",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    public static void Run()
    {
        ValidationContext context = null;

        try
        {
            context = ValidationContext.Create();

            ValidateInitialState(context);
            ValidateDistanceApi(context);
            ValidateDistanceLimitsWithoutMidpointDrift(context);
            ValidateSingleFighterDistanceLimitStability(context);
            ValidateStrafe(context);
            ValidateDodgeTransitionsAndMultipliers(context);
            ValidateDodgeCompensation(context);
            ValidateDodgeCancellation(context);
            ValidateAttackLungeInvalidation(context);
            ValidateGuardOrientationRules(context);
            ValidateAutoFaceRules(context);
            ValidateLateralMovementRestriction(context);
            ValidatePermutation(context);
            ValidateIdempotentReset(context);
            ValidateHybridGestureRecognizer();

            Debug.Log(
                "V06SpatialValidation: all automated spatial and " +
                "recognizer invariants passed."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "V06SpatialValidation failed: " + exception
            );
            throw;
        }
        finally
        {
            context?.Dispose();
        }
    }

    private static void ValidateDistanceApi(
        ValidationContext context)
    {
        CombatSpatialSettings configuration =
            context.Controller.Configuration;

        RequireNear(
            configuration.MinimumDistance,
            ExpectedCloseDistance,
            "The public minimum-distance API is incorrect."
        );
        RequireNear(
            configuration.MaximumDistance,
            ExpectedLongDistance,
            "The public maximum-distance API is incorrect."
        );
        RequireNear(
            configuration.MidRangeDistance,
            ExpectedMidDistance,
            "The public mid-distance API is incorrect."
        );
    }

    private static void ValidateInitialState(
        ValidationContext context)
    {
        CombatSpatialSnapshot snapshot =
            context.Controller.Snapshot;

        Require(
            snapshot.IsInitialized,
            "The spatial controller was not initialized."
        );
        RequireEqual(
            snapshot.Distance,
            DistanceLevel.MidRange,
            "The duel must start at Mid range."
        );
        RequireEqual(
            snapshot.Orientation,
            RelativeOrientation.Face,
            "The duel must start Face to Face."
        );
        RequireNear(
            snapshot.Separation,
            ExpectedMidDistance,
            "The initial separation must be 6 metres."
        );
        RequireVectorNear(
            Midpoint(snapshot),
            Vector3.zero,
            "Initialization must preserve the duel midpoint."
        );
    }

    private static void ValidateDistanceLimitsWithoutMidpointDrift(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        Vector3 expectedMidpoint =
            Midpoint(context.Controller.Snapshot);

        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.Advance
            ),
            "First fighter could not begin advancing."
        );
        Require(
            context.Controller.StartMovement(
                context.SecondCombat,
                SpatialMovementType.Advance
            ),
            "Second fighter could not begin advancing."
        );

        TickContinuousMovement(context.Controller, 10f);
        RequireNear(
            context.Controller.Snapshot.Separation,
            ExpectedCloseDistance,
            "Advance must stop at the Close distance limit."
        );
        RequireEqual(
            context.Controller.CurrentDistance,
            DistanceLevel.CloseRange,
            "The Close distance level was not reported."
        );
        RequireVectorNear(
            Midpoint(context.Controller.Snapshot),
            expectedMidpoint,
            "Clamping at Close range drifted the midpoint."
        );

        TickContinuousMovement(context.Controller, 1f);
        RequireVectorNear(
            Midpoint(context.Controller.Snapshot),
            expectedMidpoint,
            "Continuing to advance at the Close limit drifted " +
            "the midpoint."
        );

        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
        expectedMidpoint = Midpoint(context.Controller.Snapshot);

        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.Retreat
            ),
            "First fighter could not begin retreating."
        );
        Require(
            context.Controller.StartMovement(
                context.SecondCombat,
                SpatialMovementType.Retreat
            ),
            "Second fighter could not begin retreating."
        );

        TickContinuousMovement(context.Controller, 10f);
        RequireNear(
            context.Controller.Snapshot.Separation,
            ExpectedLongDistance,
            "Retreat must stop at the Long distance limit."
        );
        RequireEqual(
            context.Controller.CurrentDistance,
            DistanceLevel.LongRange,
            "The Long distance level was not reported."
        );
        RequireVectorNear(
            Midpoint(context.Controller.Snapshot),
            expectedMidpoint,
            "Clamping at Long range drifted the midpoint."
        );

        TickContinuousMovement(context.Controller, 1f);
        RequireVectorNear(
            Midpoint(context.Controller.Snapshot),
            expectedMidpoint,
            "Continuing to retreat at the Long limit drifted " +
            "the midpoint."
        );

        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
    }

    private static void ValidateSingleFighterDistanceLimitStability(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.Advance
            ),
            "The first fighter could not advance alone."
        );
        TickContinuousMovement(context.Controller, 10f);
        CombatSpatialSnapshot closeSnapshot =
            context.Controller.Snapshot;
        TickContinuousMovement(context.Controller, 1f);
        CombatSpatialSnapshot closeAfterExtraTick =
            context.Controller.Snapshot;
        RequireVectorNear(
            closeAfterExtraTick.FirstNeutralPose.position,
            closeSnapshot.FirstNeutralPose.position,
            "The first fighter drifted past the Close limit."
        );
        RequireVectorNear(
            closeAfterExtraTick.SecondNeutralPose.position,
            closeSnapshot.SecondNeutralPose.position,
            "The stationary fighter drifted at the Close limit."
        );

        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.Retreat
            ),
            "The first fighter could not retreat alone."
        );
        TickContinuousMovement(context.Controller, 10f);
        CombatSpatialSnapshot longSnapshot =
            context.Controller.Snapshot;
        TickContinuousMovement(context.Controller, 1f);
        CombatSpatialSnapshot longAfterExtraTick =
            context.Controller.Snapshot;
        RequireVectorNear(
            longAfterExtraTick.FirstNeutralPose.position,
            longSnapshot.FirstNeutralPose.position,
            "The first fighter drifted past the Long limit."
        );
        RequireVectorNear(
            longAfterExtraTick.SecondNeutralPose.position,
            longSnapshot.SecondNeutralPose.position,
            "The stationary fighter drifted at the Long limit."
        );

        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
    }

    private static void ValidateStrafe(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CombatSpatialSnapshot before =
            context.Controller.Snapshot;
        Vector3 midpoint = Midpoint(before);
        Vector3 startRadius =
            Horizontal(before.FirstNeutralPose.position - midpoint);
        const float deltaTime = 0.5f;

        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.StrafeLeft
            ),
            "The strafe movement could not start."
        );
        TickContinuousMovement(context.Controller, deltaTime);

        CombatSpatialSnapshot after =
            context.Controller.Snapshot;
        Vector3 endRadius =
            Horizontal(after.FirstNeutralPose.position - midpoint);
        float angle = Vector3.Angle(startRadius, endRadius);
        float arcDistance =
            startRadius.magnitude * angle * Mathf.Deg2Rad;
        float measuredSpeed = arcDistance / deltaTime;

        RequireEqual(
            after.Orientation,
            RelativeOrientation.Face,
            "Strafing must preserve Face orientation."
        );
        RequireNear(
            after.Separation,
            ExpectedMidDistance,
            "Strafing changed fighter separation."
        );
        RequireVectorNear(
            Midpoint(after),
            midpoint,
            "Strafing drifted the duel midpoint."
        );
        RequireNear(
            measuredSpeed,
            ExpectedStrafeSpeed,
            "Strafe tangential speed does not match configuration.",
            0.01f
        );

        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
    }

    private static void ValidateDodgeTransitionsAndMultipliers(
        ValidationContext context)
    {
        context.Controller.ResetDuel();

        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        RequireEqual(
            context.Controller.CurrentOrientation,
            RelativeOrientation.RightFlank,
            "First right dodge must produce RightFlank."
        );
        RequireNear(
            context.Controller.GetDamageMultiplier(
                context.FirstCombat,
                context.SecondCombat
            ),
            ExpectedFlankMultiplier,
            "Flank damage multiplier is incorrect."
        );
        RequireNear(
            context.Controller.GetDamageMultiplier(
                context.SecondCombat,
                context.FirstCombat
            ),
            1f,
            "The disadvantaged fighter received a positional bonus."
        );

        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        RequireEqual(
            context.Controller.CurrentOrientation,
            RelativeOrientation.Back,
            "A second dodge in the same direction must reach Back."
        );
        RequireNear(
            context.Controller.GetDamageMultiplier(
                context.FirstCombat,
                context.SecondCombat
            ),
            ExpectedBackMultiplier,
            "Back damage multiplier is incorrect."
        );
    }

    private static void ValidateDodgeCompensation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        CommitDodge(
            context.Controller,
            context.SecondCombat,
            DodgeDirection.Right
        );

        RequireEqual(
            context.Controller.CurrentOrientation,
            RelativeOrientation.Face,
            "Same-direction compensation by the other fighter " +
            "must restore Face."
        );
        Require(
            context.Controller.AdvantageFighter == null,
            "Compensation must clear the spatial advantage."
        );
    }

    private static void ValidateDodgeCancellation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CombatSpatialSnapshot before =
            context.Controller.Snapshot;

        Require(
            context.Controller.TryPrepareDodge(
                context.FirstCombat,
                DodgeDirection.Left,
                out SpatialDodgeTransaction transaction
            ),
            "Dodge preparation failed during cancellation test."
        );
        Require(
            context.Controller.PreviewPreparedDodge(
                transaction.Id,
                1f
            ),
            "Dodge preview failed during cancellation test."
        );
        Require(
            !VectorNear(
                context.FirstCombat.transform.position,
                before.FirstNeutralPose.position
            ),
            "The dodge preview did not move the fighter."
        );
        Require(
            context.Controller.CancelDodge(transaction),
            "Prepared dodge could not be cancelled."
        );

        CombatSpatialSnapshot after =
            context.Controller.Snapshot;
        RequireEqual(
            after.Orientation,
            RelativeOrientation.Face,
            "Cancelling a dodge changed the validated orientation."
        );
        RequireVectorNear(
            after.FirstNeutralPose.position,
            before.FirstNeutralPose.position,
            "Cancelling a dodge changed the first neutral pose."
        );
        RequireVectorNear(
            after.SecondNeutralPose.position,
            before.SecondNeutralPose.position,
            "Cancelling a dodge changed the second neutral pose."
        );
        RequireVectorNear(
            context.FirstCombat.transform.position,
            before.FirstNeutralPose.position,
            "Cancelling a dodge did not restore the first transform."
        );
        RequireVectorNear(
            context.SecondCombat.transform.position,
            before.SecondNeutralPose.position,
            "Cancelling a dodge did not restore the second transform."
        );
    }

    private static void ValidateGuardOrientationRules(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();
        context.FirstStats.ResetStats();
        context.SecondStats.ResetStats();

        float simpleGuardStamina =
            context.FirstStats.CurrentStamina;
        RequireEqual(
            context.FirstCombat.StartDefense(),
            CombatActionResult.Started,
            "Simple guard must be allowed from Face."
        );
        Require(
            context.FirstStats.CurrentStamina <
            simpleGuardStamina,
            "Simple guard from Face did not spend its normal cost."
        );
        context.FirstCombat.CancelActiveActions(false);
        context.FirstCombat.ResetCombatState();
        context.FirstStats.ResetStats();

        float heldGuardStamina =
            context.FirstStats.CurrentStamina;
        RequireEqual(
            context.FirstCombat.StartHeldGuard(),
            CombatActionResult.Started,
            "Held guard must be allowed from Face."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            heldGuardStamina,
            "Starting held guard spent stamina immediately."
        );
        context.FirstCombat.StopHeldGuard();
        context.FirstCombat.ResetCombatState();

        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        AssertGuardsRefusedWithoutCost(
            context.SecondCombat,
            context.SecondStats,
            CombatRefusalReason.FlankGuardForbidden,
            "flank"
        );

        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        AssertGuardsRefusedWithoutCost(
            context.SecondCombat,
            context.SecondStats,
            CombatRefusalReason.BackGuardForbidden,
            "back"
        );

        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();
    }

    private static void ValidateAttackLungeInvalidation(
        ValidationContext context)
    {
        if (MoveAttackTowardTargetMethod == null)
        {
            throw new MissingMethodException(
                nameof(FighterCombat),
                "MoveAttackTowardTarget"
            );
        }

        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();
        context.SecondStats.ResetStats();
        int expectedImpactRevision =
            context.SecondCombat.IncomingImpactRevision;

        IEnumerator lunge =
            MoveAttackTowardTargetMethod.Invoke(
                context.FirstCombat,
                new object[] { 1f, expectedImpactRevision }
            ) as IEnumerator;
        Require(
            lunge != null && lunge.MoveNext(),
            "The attack lunge did not begin."
        );

        RequireEqual(
            context.SecondCombat.TryPermutation(1),
            CombatActionResult.Started,
            "The target could not permute during the lunge."
        );
        Require(
            !lunge.MoveNext(),
            "A stale attack lunge survived target permutation."
        );
        Require(
            context.Controller.TryGetNeutralPosition(
                context.FirstCombat,
                out Vector3 firstNeutralPosition
            ),
            "The first neutral position was unavailable."
        );
        RequireVectorNear(
            context.FirstCombat.transform.position,
            firstNeutralPosition,
            "A stale attack lunge overwrote the permuted pose."
        );

        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();
        context.Controller.ResetDuel();
    }

    private static void ValidateAutoFaceRules(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();

        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        TickAutoFace(context.Controller, 3.01f);
        RequireEqual(
            context.Controller.CurrentOrientation,
            RelativeOrientation.Face,
            "A flank did not auto-face after three idle seconds."
        );

        context.Controller.ResetDuel();
        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Right
        );
        TickAutoFace(context.Controller, 10f);
        RequireEqual(
            context.Controller.CurrentOrientation,
            RelativeOrientation.Back,
            "Back orientation must never auto-face."
        );

        context.Controller.ResetDuel();
    }

    private static void ValidateLateralMovementRestriction(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CommitDodge(
            context.Controller,
            context.FirstCombat,
            DodgeDirection.Left
        );

        RequireEqual(
            context.FirstCombat.StartSpatialMovement(
                SpatialMovementType.StrafeLeft
            ),
            CombatActionResult.Unavailable,
            "Lateral movement must be refused outside Face."
        );
        RequireEqual(
            context.FirstCombat.LastRefusalReason,
            CombatRefusalReason.IncompatibleOrientation,
            "Lateral refusal reported the wrong reason."
        );
        RequireEqual(
            context.Controller.Snapshot.FirstMovement,
            SpatialMovementType.None,
            "A refused lateral movement remained active."
        );

        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
    }

    private static void ValidatePermutation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.FirstStats.SetStamina(49f);
        float staminaBeforeRefusal =
            context.FirstStats.CurrentStamina;

        RequireEqual(
            context.FirstCombat.TryPermutation(10),
            CombatActionResult.NotEnoughStamina,
            "Permutation with 49 stamina must be refused."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            staminaBeforeRefusal,
            "Refused permutation spent stamina."
        );
        RequireEqual(
            context.FirstCombat.LastRefusalReason,
            CombatRefusalReason.NotEnoughStamina,
            "Permutation with 49 stamina reported the wrong reason."
        );

        context.FirstStats.SetStamina(50f);
        Vector3 firstBefore =
            context.Controller.FirstNeutralPosition;
        Vector3 secondBefore =
            context.Controller.SecondNeutralPosition;
        float staminaBefore = context.FirstStats.CurrentStamina;

        RequireNear(
            context.FirstCombat.Rules
                .ResolvePermutationStaminaCost(),
            ExpectedPermutationCost,
            "The configured permutation cost must be 50."
        );
        RequireEqual(
            context.FirstCombat.TryPermutation(11),
            CombatActionResult.Started,
            "FighterCombat.TryPermutation did not start."
        );

        CombatSpatialSnapshot after =
            context.Controller.Snapshot;
        RequireNear(
            staminaBefore - context.FirstStats.CurrentStamina,
            ExpectedPermutationCost,
            "Permutation did not spend exactly 50 stamina."
        );
        RequireEqual(
            after.Distance,
            DistanceLevel.MidRange,
            "Permutation must restore Mid range."
        );
        RequireEqual(
            after.Orientation,
            RelativeOrientation.Face,
            "Permutation must restore Face orientation."
        );
        RequireNear(
            after.Separation,
            ExpectedMidDistance,
            "Permutation must restore 6 metres of separation."
        );
        RequireVectorNear(
            after.FirstNeutralPose.position,
            secondBefore,
            "Permutation did not move the first fighter to the " +
            "opposite side."
        );
        RequireVectorNear(
            after.SecondNeutralPose.position,
            firstBefore,
            "Permutation did not move the second fighter to the " +
            "opposite side."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            0f,
            "Permutation from exactly 50 stamina must end at zero."
        );
        RequireEqual(
            context.FirstCombat.CurrentState,
            FighterCombatState.Idle,
            "Permutation must not stun the fighter."
        );
        RequireEqual(
            context.FirstCombat.CurrentStunReason,
            FighterStunReason.None,
            "Permutation assigned an unexpected stun reason."
        );

        float staminaBeforeDuplicate =
            context.FirstStats.CurrentStamina;
        RequireEqual(
            context.FirstCombat.TryPermutation(11),
            CombatActionResult.Unavailable,
            "A duplicate permutation token must be refused."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            staminaBeforeDuplicate,
            "A duplicate permutation token spent stamina."
        );
        RequireEqual(
            context.FirstCombat.LastRefusalReason,
            CombatRefusalReason.DuplicateCommand,
            "Duplicate permutation reported the wrong refusal."
        );
    }

    private static void ValidateIdempotentReset(
        ValidationContext context)
    {
        long epochBeforeReset =
            context.Controller.Snapshot.DodgeEpoch;
        context.Controller.ResetDuel();
        int revisionAfterFirstReset =
            context.Controller.Revision;
        long epochAfterFirstReset =
            context.Controller.Snapshot.DodgeEpoch;

        Require(
            epochAfterFirstReset > epochBeforeReset,
            "An effective duel reset did not advance the epoch."
        );

        context.Controller.ResetDuel();

        Require(
            context.Controller.Revision == revisionAfterFirstReset,
            "A second ResetDuel call changed the spatial revision."
        );
        Require(
            context.Controller.Snapshot.DodgeEpoch ==
                epochAfterFirstReset,
            "An idempotent ResetDuel call changed the epoch."
        );
    }

    private static void ValidateHybridGestureRecognizer()
    {
        HybridGestureRecognizer recognizer =
            new(new HybridGestureRecognizerSettings());

        GestureRecognitionResult diagonal = recognizer.Recognize(
            BuildGesture(0, 8)
        );
        Require(
            !diagonal.IsRecognized,
            "Unassigned A->I was falsely recognized as a command."
        );
        RequireZones(
            diagonal.Zones,
            new[] { 0, 8 },
            "A->I zone projection changed."
        );

        GestureRecognitionResult dodgeRight = recognizer.Recognize(
            BuildGesture(6, 7, 8)
        );
        RequireEqual(
            dodgeRight.GestureId,
            CombatGestureId.DodgeRight,
            "G->H->I must be recognized as DodgeRight."
        );
        Require(
            dodgeRight.IsRecognized,
            "G->H->I was not recognized."
        );

        GestureRecognitionResult dodgeLeft = recognizer.Recognize(
            BuildGesture(8, 7, 6)
        );
        RequireEqual(
            dodgeLeft.GestureId,
            CombatGestureId.DodgeLeft,
            "I->H->G must be recognized as DodgeLeft."
        );
        Require(
            dodgeLeft.IsRecognized,
            "I->H->G was not recognized."
        );

        GestureRecognitionResult grandV = recognizer.Recognize(
            BuildGesture(0, 7, 2)
        );
        RequireEqual(
            grandV.GestureId,
            CombatGestureId.GrandV,
            "A->H->C must be recognized as GrandV."
        );
        Require(
            grandV.IsRecognized,
            "A->H->C was not recognized."
        );
    }

    private static IReadOnlyList<TimedGestureSample> BuildGesture(
        params int[] zones)
    {
        List<TimedGestureSample> samples =
            new(zones.Length);

        for (int index = 0; index < zones.Length; index++)
        {
            samples.Add(
                new TimedGestureSample(
                    HybridGestureRecognizer.GetZoneCenter(
                        zones[index],
                        0.1f
                    ),
                    index * 0.15f
                )
            );
        }

        return samples;
    }

    private static void CommitDodge(
        CombatSpatialController controller,
        FighterCombat fighter,
        DodgeDirection direction)
    {
        Require(
            controller.TryPrepareDodge(
                fighter,
                direction,
                out SpatialDodgeTransaction transaction
            ),
            "Dodge preparation failed for " + direction + "."
        );
        Require(
            controller.PreviewPreparedDodge(
                transaction.Id,
                1f
            ),
            "Dodge preview failed for " + direction + "."
        );
        Require(
            controller.CommitDodge(transaction),
            "Dodge commit failed for " + direction + "."
        );
    }

    private static void TickContinuousMovement(
        CombatSpatialController controller,
        float deltaTime)
    {
        if (UpdateContinuousMovementMethod == null)
        {
            throw new MissingMethodException(
                nameof(CombatSpatialController),
                "UpdateContinuousMovement"
            );
        }

        try
        {
            UpdateContinuousMovementMethod.Invoke(
                controller,
                new object[] { deltaTime }
            );
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static void TickAutoFace(
        CombatSpatialController controller,
        float deltaTime)
    {
        InvokePrivateDeltaMethod(
            UpdateAutoFaceMethod,
            controller,
            deltaTime,
            "UpdateAutoFace"
        );
    }

    private static void AssertGuardsRefusedWithoutCost(
        FighterCombat fighter,
        FighterStats stats,
        CombatRefusalReason expectedReason,
        string orientationName)
    {
        stats.ResetStats();
        float staminaBefore = stats.CurrentStamina;

        RequireEqual(
            fighter.StartDefense(),
            CombatActionResult.Unavailable,
            "Simple guard must be refused from " +
            orientationName + "."
        );
        RequireNear(
            stats.CurrentStamina,
            staminaBefore,
            "Simple guard refusal from " + orientationName +
            " spent stamina."
        );
        RequireEqual(
            fighter.LastRefusalReason,
            expectedReason,
            "Simple guard refusal from " + orientationName +
            " reported the wrong reason."
        );

        RequireEqual(
            fighter.StartHeldGuard(),
            CombatActionResult.Unavailable,
            "Held guard must be refused from " +
            orientationName + "."
        );
        RequireNear(
            stats.CurrentStamina,
            staminaBefore,
            "Held guard refusal from " + orientationName +
            " spent stamina."
        );
        RequireEqual(
            fighter.LastRefusalReason,
            expectedReason,
            "Held guard refusal from " + orientationName +
            " reported the wrong reason."
        );
    }

    private static void InvokePrivateDeltaMethod(
        MethodInfo method,
        CombatSpatialController controller,
        float deltaTime,
        string methodName)
    {
        if (method == null)
        {
            throw new MissingMethodException(
                nameof(CombatSpatialController),
                methodName
            );
        }

        try
        {
            method.Invoke(
                controller,
                new object[] { deltaTime }
            );
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static Vector3 Midpoint(
        CombatSpatialSnapshot snapshot)
    {
        return (
            snapshot.FirstNeutralPose.position +
            snapshot.SecondNeutralPose.position
        ) * 0.5f;
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static bool VectorNear(
        Vector3 actual,
        Vector3 expected,
        float tolerance = PositionTolerance)
    {
        return Vector3.Distance(actual, expected) <= tolerance;
    }

    private static void RequireVectorNear(
        Vector3 actual,
        Vector3 expected,
        string message,
        float tolerance = PositionTolerance)
    {
        if (VectorNear(actual, expected, tolerance))
            return;

        throw new InvalidOperationException(
            message + " Expected " + expected +
            ", received " + actual + "."
        );
    }

    private static void RequireNear(
        float actual,
        float expected,
        string message,
        float tolerance = ScalarTolerance)
    {
        if (Mathf.Abs(actual - expected) <= tolerance)
            return;

        throw new InvalidOperationException(
            message + " Expected " + expected +
            ", received " + actual + "."
        );
    }

    private static void RequireEqual<T>(
        T actual,
        T expected,
        string message)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected))
            return;

        throw new InvalidOperationException(
            message + " Expected " + expected +
            ", received " + actual + "."
        );
    }

    private static void RequireZones(
        IReadOnlyList<int> actual,
        IReadOnlyList<int> expected,
        string message)
    {
        if (actual == null || actual.Count != expected.Count)
        {
            throw new InvalidOperationException(
                message + " Unexpected zone count."
            );
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index] == expected[index])
                continue;

            throw new InvalidOperationException(
                message + " Difference at index " + index + "."
            );
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ValidationContext : IDisposable
    {
        private readonly GameObject controllerObject;
        private readonly GameObject firstObject;
        private readonly GameObject secondObject;

        public CombatSpatialController Controller { get; }
        public FighterCombat FirstCombat { get; }
        public FighterCombat SecondCombat { get; }
        public FighterStats FirstStats { get; }
        public FighterStats SecondStats { get; }

        private ValidationContext(
            GameObject controllerObject,
            GameObject firstObject,
            GameObject secondObject,
            CombatSpatialController controller,
            FighterCombat firstCombat,
            FighterCombat secondCombat,
            FighterStats firstStats,
            FighterStats secondStats)
        {
            this.controllerObject = controllerObject;
            this.firstObject = firstObject;
            this.secondObject = secondObject;
            Controller = controller;
            FirstCombat = firstCombat;
            SecondCombat = secondCombat;
            FirstStats = firstStats;
            SecondStats = secondStats;
        }

        public static ValidationContext Create()
        {
            GameObject firstObject =
                CreateHiddenObject("V06 Validation First");
            GameObject secondObject =
                CreateHiddenObject("V06 Validation Second");
            GameObject controllerObject =
                CreateHiddenObject("V06 Validation Spatial");

            firstObject.transform.position =
                new Vector3(-3f, 0f, 0f);
            secondObject.transform.position =
                new Vector3(3f, 0f, 0f);

            FighterStats firstStats =
                firstObject.AddComponent<FighterStats>();
            FighterStats secondStats =
                secondObject.AddComponent<FighterStats>();
            firstStats.ResetStats();
            secondStats.ResetStats();

            FighterCombat firstCombat =
                firstObject.AddComponent<FighterCombat>();
            FighterCombat secondCombat =
                secondObject.AddComponent<FighterCombat>();

            WireCombat(
                firstCombat,
                firstStats,
                secondStats,
                secondCombat
            );
            WireCombat(
                secondCombat,
                secondStats,
                firstStats,
                firstCombat
            );

            CombatSpatialController controller =
                controllerObject
                    .AddComponent<CombatSpatialController>();
            controller.Configure(CreateSettings());
            Require(
                controller.Initialize(firstCombat, secondCombat),
                "Unable to initialize spatial validation duel."
            );
            firstCombat.SetSpatialController(controller);
            secondCombat.SetSpatialController(controller);

            return new ValidationContext(
                controllerObject,
                firstObject,
                secondObject,
                controller,
                firstCombat,
                secondCombat,
                firstStats,
                secondStats
            );
        }

        public void Dispose()
        {
            if (firstObject != null)
                UnityEngine.Object.DestroyImmediate(firstObject);
            if (secondObject != null)
                UnityEngine.Object.DestroyImmediate(secondObject);
            if (controllerObject != null)
                UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        private static GameObject CreateHiddenObject(string name)
        {
            GameObject instance = new(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return instance;
        }

        private static CombatSpatialSettings CreateSettings()
        {
            return new CombatSpatialSettings
            {
                MinimumDistance = ExpectedCloseDistance,
                CloseRangeUpperBound = 4.25f,
                MidRangeDistance = ExpectedMidDistance,
                MidRangeUpperBound = 7.25f,
                MaximumDistance = ExpectedLongDistance,
                AdvanceSpeed = 2.5f,
                RetreatSpeed = 2f,
                StrafeSpeed = ExpectedStrafeSpeed,
                RotationSpeed = 540f,
                DodgeOrientationAngle = 90f,
                AutoFaceFlanks = true,
                FlankAutoFaceDelay = 3f,
                FaceDamageMultiplier = 1f,
                FlankDamageMultiplier =
                    ExpectedFlankMultiplier,
                BackDamageMultiplier =
                    ExpectedBackMultiplier
            };
        }

        private static void WireCombat(
            FighterCombat combat,
            FighterStats ownStats,
            FighterStats targetStats,
            FighterCombat targetCombat)
        {
            SerializedObject serialized =
                new(combat);
            serialized.Update();
            serialized.FindProperty("fighterStats")
                .objectReferenceValue = ownStats;
            serialized.FindProperty("targetStats")
                .objectReferenceValue = targetStats;
            serialized.FindProperty("targetCombat")
                .objectReferenceValue = targetCombat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
