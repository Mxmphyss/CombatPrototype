using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class V061CorrectionValidation
{
    private const float Tolerance = 0.002f;

    public static void Run()
    {
        ValidationContext context = null;
        try
        {
            context = ValidationContext.Create();
            ValidateDiscreteDistances(context);
            ValidateTransactionalCancellation(context);
            ValidateLateralOrientation(context);
            ValidateSymmetricAttackOrientation(context);
            ValidateDodgeTimingWindows(context);
            ValidateDodgePreservesOtherAnimation(context);
            ValidateAutoFaceDuringActions(context);
            ValidateCyclicPermutation(context);
            ValidateGestureShapes();
            ValidateCameraReset(context);
            ValidateDistanceVisuals(context);
            Debug.Log(
                "V061CorrectionValidation: all automated camera, " +
                "distance, dodge, permutation and gesture invariants passed."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "V061CorrectionValidation failed: " + exception
            );
            throw;
        }
        finally
        {
            context?.Dispose();
        }
    }

    private static void ValidateDiscreteDistances(
        ValidationContext context)
    {
        RequireDistance(
            context,
            DistanceLevel.MidRange,
            6f,
            "The duel must start at MidRange."
        );
        CommitDodge(
            context,
            DodgeDirection.Forward,
            DistanceLevel.CloseRange
        );
        Require(
            !context.Controller.CanDodge(
                context.FirstCombat,
                DodgeDirection.Forward
            ),
            "Forward dodge must stop at CloseRange."
        );
        CommitDodge(
            context,
            DodgeDirection.Backward,
            DistanceLevel.MidRange
        );
        CommitDodge(
            context,
            DodgeDirection.Backward,
            DistanceLevel.LongRange
        );
        Require(
            !context.Controller.CanDodge(
                context.FirstCombat,
                DodgeDirection.Backward
            ),
            "Backward dodge must stop at LongRange."
        );
        CommitDodge(
            context,
            DodgeDirection.Forward,
            DistanceLevel.MidRange
        );
    }

    private static void ValidateTransactionalCancellation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        Require(
            context.Controller.TryPrepareDodge(
                context.FirstCombat,
                DodgeDirection.Forward,
                out SpatialDodgeTransaction transaction
            ),
            "Unable to prepare the transactional forward dodge."
        );
        Require(
            context.Controller.PreviewPreparedDodge(
                transaction.Id,
                0.5f
            ),
            "Unable to preview the transactional dodge."
        );
        Require(
            context.Controller.CancelDodge(transaction),
            "Unable to cancel the transactional dodge."
        );
        RequireDistance(
            context,
            DistanceLevel.MidRange,
            6f,
            "A cancelled dodge must restore the validated anchor."
        );
    }

    private static void ValidateLateralOrientation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CommitDodge(
            context,
            DodgeDirection.Left,
            DistanceLevel.MidRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.LeftFlank,
            "The first lateral dodge must still create a flank."
        );
        RequireNear(
            context.Controller.Snapshot.Separation,
            6f,
            "A lateral dodge must preserve the distance anchor."
        );
        Quaternion firstFlankRotation =
            context.Controller.Snapshot.FirstNeutralPose.rotation;
        Quaternion secondFlankRotation =
            context.Controller.Snapshot.SecondNeutralPose.rotation;
        CommitDodge(
            context,
            DodgeDirection.Forward,
            DistanceLevel.CloseRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.LeftFlank,
            "A radial dodge erased the flank state."
        );
        Require(
            Quaternion.Angle(
                firstFlankRotation,
                context.Controller.Snapshot.FirstNeutralPose.rotation
            ) <= 0.001f &&
            Quaternion.Angle(
                secondFlankRotation,
                context.Controller.Snapshot.SecondNeutralPose.rotation
            ) <= 0.001f,
            "A radial dodge auto-rotated a flank."
        );
        CommitDodge(
            context,
            DodgeDirection.Left,
            DistanceLevel.CloseRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.Back,
            "A radial dodge erased the lateral sequence toward Back."
        );
        Require(
            context.Controller.CanDodge(
                context.FirstCombat,
                DodgeDirection.Left
            ) &&
            context.Controller.CanDodge(
                context.FirstCombat,
                DodgeDirection.Right
            ),
            "A fighter must be able to dodge laterally from Back."
        );
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.CloseRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.RightFlank,
            "A right dodge from Back must reach the right flank."
        );

        context.Controller.ResetDuel();
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.MidRange
        );
        Require(
            context.Controller.CanDodge(
                context.SecondCombat,
                DodgeDirection.Left
            ),
            "The disadvantaged fighter must be able to dodge " +
            "in either lateral direction."
        );
        Require(
            context.Controller.TryPrepareDodge(
                context.SecondCombat,
                DodgeDirection.Left,
                out SpatialDodgeTransaction counterDodge
            ),
            "Unable to prepare the disadvantaged fighter dodge."
        );
        context.Controller.PreviewPreparedDodge(
            counterDodge.Id,
            1f
        );
        Require(
            context.Controller.CommitDodge(counterDodge),
            "Unable to commit the disadvantaged fighter dodge."
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.Back &&
            context.Controller.AdvantageFighter ==
                context.SecondCombat,
            "The counter-dodge must grant the new back advantage."
        );
        context.Controller.ResetDuel();
    }

    private static void ValidateSymmetricAttackOrientation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.MidRange
        );

        Require(
            context.Controller.CanAttackTarget(
                context.FirstCombat,
                context.SecondCombat
            ),
            "The fighter with flank advantage must be able to attack."
        );
        Require(
            context.Controller.CanAttackTarget(
                context.SecondCombat,
                context.FirstCombat
            ),
            "A fighter must be allowed to attack in its facing direction."
        );
        Require(
            context.FirstCombat.CanHitCurrentTarget(),
            "The fighter facing the flank target must be able to hit."
        );
        Require(
            !context.SecondCombat.CanHitCurrentTarget(),
            "A forward attack incorrectly hit a target on the flank."
        );

        Require(
            context.Controller.TryPrepareDodge(
                context.SecondCombat,
                DodgeDirection.Right,
                out SpatialDodgeTransaction compensation
            ),
            "Unable to prepare the opponent facing compensation."
        );
        context.Controller.PreviewPreparedDodge(
            compensation.Id,
            1f
        );
        Require(
            context.Controller.CommitDodge(compensation),
            "Unable to commit the opponent facing compensation."
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.Face &&
            context.FirstCombat.CanHitCurrentTarget() &&
            context.SecondCombat.CanHitCurrentTarget(),
            "Both forward attack arcs must contain the target after facing."
        );

        context.Controller.ResetDuel();
    }

    private static void ValidateDodgeTimingWindows(
        ValidationContext context)
    {
        FighterCombat fighter = context.FirstCombat;
        RequireNear(
            fighter.DodgeStartupDuration,
            0.08f,
            "The dodge vulnerable startup must be 0.08 seconds."
        );
        RequireNear(
            fighter.DodgeInvulnerabilityDuration,
            0.24f,
            "The dodge invulnerability must be 0.24 seconds."
        );
        RequireNear(
            fighter.PerfectDodgeWindow,
            0.1f,
            "The perfect dodge window must be 0.10 seconds."
        );
        RequireNear(
            fighter.DodgeRecoveryDuration,
            0.12f,
            "The dodge recovery must be 0.12 seconds."
        );

        Require(
            fighter.GetDodgeWindowPhase(0.04f) ==
                DodgeWindowPhase.StartupVulnerable,
            "An early dodge impact must remain vulnerable."
        );
        Require(
            fighter.GetDodgeWindowPhase(0.09f) ==
                DodgeWindowPhase.Invulnerable,
            "The dodge did not enter its invulnerability window."
        );
        Require(
            fighter.GetDodgeWindowPhase(0.2f) ==
                DodgeWindowPhase.Perfect,
            "The centre of the dodge is not the perfect window."
        );
        Require(
            fighter.GetDodgeWindowPhase(0.31f) ==
                DodgeWindowPhase.Invulnerable,
            "The end of the invulnerability window is incorrect."
        );
        Require(
            fighter.GetDodgeWindowPhase(0.33f) ==
                DodgeWindowPhase.RecoveryVulnerable,
            "A late dodge impact must be vulnerable."
        );
    }

    private static void ValidateDodgePreservesOtherAnimation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        Vector3 animatedOpponentPosition =
            context.SecondCombat.transform.position +
            context.SecondCombat.transform.forward * 0.4f;
        context.SecondCombat.transform.position =
            animatedOpponentPosition;

        Require(
            context.Controller.TryPrepareDodge(
                context.FirstCombat,
                DodgeDirection.Left,
                out SpatialDodgeTransaction transaction
            ),
            "Unable to prepare the dodge animation isolation test."
        );
        Require(
            context.Controller.PreviewPreparedDodge(
                transaction.Id,
                0.5f
            ),
            "Unable to preview the dodge animation isolation test."
        );
        Require(
            Vector3.Distance(
                animatedOpponentPosition,
                context.SecondCombat.transform.position
            ) <= Tolerance,
            "Dodge preview erased the opponent attack animation."
        );
        context.Controller.PreviewPreparedDodge(
            transaction.Id,
            1f
        );
        Require(
            context.Controller.CommitDodge(transaction),
            "Unable to commit the dodge animation isolation test."
        );
        Require(
            Vector3.Distance(
                animatedOpponentPosition,
                context.SecondCombat.transform.position
            ) <= Tolerance,
            "Dodge commit visibly corrected the opponent animation."
        );

        context.Controller.RestoreNeutralPose(
            context.SecondCombat
        );
        context.Controller.ResetDuel();
    }

    private static void ValidateAutoFaceDuringActions(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        context.FirstCombat.ResetCombatState();
        context.SecondCombat.ResetCombatState();
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.MidRange
        );

        Require(
            context.SecondCombat.StartCharge() ==
                CombatActionResult.Started,
            "Unable to start the action-safe auto-face test."
        );
        Invoke(
            context.Controller,
            "UpdateAutoFace",
            3.01f
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.RightFlank,
            "Auto-face rotated the duel during an active action."
        );

        context.SecondCombat.StopChargeInput();
        Invoke(
            context.Controller,
            "UpdateAutoFace",
            0f
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.Face,
            "The elapsed flank timer did not auto-face at the next safe frame."
        );

        context.Controller.ResetDuel();
    }

    private static void ValidateCyclicPermutation(
        ValidationContext context)
    {
        context.Controller.ResetDuel();
        context.FirstStats.SetStamina(100f);
        Require(
            context.FirstCombat.TryPermutation(1) ==
                CombatActionResult.Started,
            "Mid-to-Long permutation was refused."
        );
        RequireDistance(
            context,
            DistanceLevel.LongRange,
            9f,
            "MidRange must permute to LongRange."
        );

        context.FirstStats.SetStamina(100f);
        Require(
            context.FirstCombat.TryPermutation(2) ==
                CombatActionResult.Started,
            "Long-to-Close permutation was refused."
        );
        RequireDistance(
            context,
            DistanceLevel.CloseRange,
            3f,
            "LongRange must permute to CloseRange."
        );

        context.FirstStats.SetStamina(50f);
        Require(
            context.FirstCombat.TryPermutation(3) ==
                CombatActionResult.Started,
            "Close-to-Mid permutation was refused."
        );
        RequireDistance(
            context,
            DistanceLevel.MidRange,
            6f,
            "CloseRange must permute to MidRange."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            0f,
            "Exactly 50 stamina must leave zero after permutation."
        );
        Require(
            context.FirstCombat.CurrentState ==
                FighterCombatState.Idle &&
            context.FirstCombat.CurrentStunReason ==
                FighterStunReason.None,
            "A zero-stamina permutation created an unintended stun."
        );

        context.FirstStats.SetStamina(49f);
        Require(
            context.FirstCombat.TryPermutation(4) ==
                CombatActionResult.NotEnoughStamina,
            "Permutation with less than 50 stamina was accepted."
        );
        RequireDistance(
            context,
            DistanceLevel.MidRange,
            6f,
            "A refused permutation changed the distance."
        );
        RequireNear(
            context.FirstStats.CurrentStamina,
            49f,
            "A refused permutation spent stamina."
        );

        context.Controller.ResetDuel();
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.MidRange
        );
        Quaternion firstFlankRotation =
            context.Controller.Snapshot.FirstNeutralPose.rotation;
        Quaternion secondFlankRotation =
            context.Controller.Snapshot.SecondNeutralPose.rotation;
        context.FirstStats.SetStamina(100f);
        Require(
            context.FirstCombat.TryPermutation(5) ==
                CombatActionResult.Started,
            "A permutation from the right flank was refused."
        );
        RequireDistance(
            context,
            DistanceLevel.LongRange,
            9f,
            "A flank permutation did not change distance."
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.RightFlank &&
            context.Controller.AdvantageFighter ==
                context.FirstCombat,
            "A permutation erased the existing flank advantage."
        );
        Require(
            Quaternion.Angle(
                firstFlankRotation,
                context.Controller.Snapshot.FirstNeutralPose.rotation
            ) <= Tolerance &&
            Quaternion.Angle(
                secondFlankRotation,
                context.Controller.Snapshot.SecondNeutralPose.rotation
            ) <= Tolerance,
            "A flank permutation forced the fighters to face."
        );
        CommitDodge(
            context,
            DodgeDirection.Right,
            DistanceLevel.LongRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.Back &&
            context.Controller.AdvantageFighter ==
                context.FirstCombat,
            "Permutation did not preserve the lateral sequence."
        );

        context.Controller.ResetDuel();
    }

    private static void ValidateGestureShapes()
    {
        HybridGestureRecognizer recognizer =
            new(new HybridGestureRecognizerSettings(), 0.1f);
        GestureRecognitionResult direct = recognizer.Recognize(
            CreateStroke(6, 4, 8)
        );
        Require(
            direct.IsRecognized &&
            direct.GestureId == CombatGestureId.Permutation,
            "G-E-I was not recognized as Permutation. " +
            $"Status={direct.Status}, Gesture={direct.GestureId}, " +
            $"Confidence={direct.Confidence:F3}."
        );

        GestureRecognitionResult tolerant = recognizer.Recognize(
            CreateStroke(6, 3, 4, 5, 8)
        );
        Require(
            tolerant.IsRecognized &&
            tolerant.GestureId == CombatGestureId.Permutation,
            "G-D-E-F-I was not normalized to Permutation."
        );
        RequireZones(
            tolerant.Zones,
            6,
            4,
            8
        );
        RequireZones(
            tolerant.RawZones,
            6,
            3,
            4,
            5,
            8
        );

        GestureRecognitionResult horizontal = recognizer.Recognize(
            CreateStroke(6, 7, 8)
        );
        Require(
            horizontal.IsRecognized &&
            horizontal.GestureId == CombatGestureId.DodgeRight,
            "G-H-I no longer resolves to the right dodge."
        );
        GestureRecognitionResult reverseHorizontal =
            recognizer.Recognize(
                CreateStroke(8, 7, 6)
            );
        Require(
            reverseHorizontal.IsRecognized &&
            reverseHorizontal.GestureId ==
                CombatGestureId.DodgeLeft,
            "I-H-G no longer resolves to the left dodge."
        );

        GestureRecognitionResult upperRoof = recognizer.Recognize(
            CreateStroke(0, 4, 2)
        );
        Require(
            upperRoof.GestureId != CombatGestureId.Permutation,
            "An upper-pad roof became a permutation."
        );

        GestureRecognitionResult grandV = recognizer.Recognize(
            CreateStroke(0, 7, 2)
        );
        Require(
            grandV.IsRecognized &&
            grandV.GestureId == CombatGestureId.GrandV,
            "A-H-C grand V regressed."
        );
    }

    private static void ValidateCameraReset(
        ValidationContext context)
    {
        GameObject cameraObject =
            ValidationContext.CreateHiddenObject(
                "V061 Validation Camera"
            );
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, 8f, -10f),
            Quaternion.Euler(28f, 0f, 0f)
        );
        CombatCameraController controller =
            cameraObject.AddComponent<CombatCameraController>();
        controller.Initialize(
            camera,
            context.FirstCombat,
            context.SecondCombat,
            context.Controller
        );
        bool multiTouchEventRaised = false;
        controller.OnMultiTouchStateChanged += active =>
            multiTouchEventRaised |= active;
        Invoke(controller, "SetMultiTouchActive", true);
        Require(
            controller.IsMultiTouchActive &&
            multiTouchEventRaised,
            "Camera did not reserve input for multi-touch."
        );
        controller.CancelTransientInput();
        Require(
            !controller.IsMultiTouchActive,
            "Camera did not release multi-touch input."
        );

        Invoke(
            controller,
            "ApplyManualPanDelta",
            new Vector2(0.2f, -0.1f)
        );
        Invoke(controller, "ApplyPinchDelta", 0.2f);
        Require(
            controller.IsManualViewActive,
            "Manual camera offsets were not recorded."
        );
        controller.ResetCameraView(true);
        Require(
            !controller.IsManualViewActive,
            "Camera reset did not clear manual offsets."
        );
        Require(
            camera.fieldOfView >= 28f &&
            camera.fieldOfView <= 72f,
            "Camera zoom escaped its configured limits."
        );
        Quaternion initialRotation = camera.transform.rotation;
        Vector3 initialPlayerViewport =
            camera.WorldToViewportPoint(
                context.FirstCombat.transform.position
            );
        RequirePairVisible(camera, context, "MidRange");
        RequireCameraBehindPlayer(
            camera,
            context,
            "MidRange"
        );

        CommitDodge(
            context,
            DodgeDirection.Forward,
            DistanceLevel.CloseRange
        );
        controller.ResetCameraView(true);
        RequirePairVisible(camera, context, "CloseRange");

        CommitDodge(
            context,
            DodgeDirection.Backward,
            DistanceLevel.MidRange
        );
        CommitDodge(
            context,
            DodgeDirection.Backward,
            DistanceLevel.LongRange
        );
        controller.ResetCameraView(true);
        RequirePairVisible(camera, context, "LongRange");

        context.Controller.ResetDuel();
        CommitDodge(
            context,
            DodgeDirection.Left,
            DistanceLevel.MidRange
        );
        Require(
            context.Controller.CurrentOrientation ==
                RelativeOrientation.LeftFlank,
            "Lateral dodge did not reach the left flank."
        );
        controller.ResetCameraView(true);
        RequirePairVisible(camera, context, "left flank");
        RequireCameraBehindPlayer(
            camera,
            context,
            "left flank"
        );
        RequirePlayerScreenAnchor(
            camera,
            context,
            initialPlayerViewport,
            "left flank"
        );
        Require(
            Quaternion.Angle(
                initialRotation,
                camera.transform.rotation
            ) >= 1f,
            "Camera did not follow the player to the left flank."
        );

        context.Controller.ResetDuel();
        controller.ResetCameraView(true);
        Vector3 stationaryOpponentPosition =
            context.SecondCombat.transform.position;
        Require(
            context.Controller.StartMovement(
                context.FirstCombat,
                SpatialMovementType.StrafeLeft
            ),
            "Unable to start the camera strafe check."
        );
        Invoke(
            context.Controller,
            "UpdateContinuousMovement",
            12f
        );
        Require(
            Vector3.Distance(
                stationaryOpponentPosition,
                context.SecondCombat.transform.position
            ) <= Tolerance,
            "The stationary opponent moved during a player strafe."
        );
        RequireNear(
            context.Controller.Snapshot.Separation,
            6f,
            "Strafe changed the active distance anchor."
        );
        Vector3 firstToSecond = (
            context.SecondCombat.transform.position -
            context.FirstCombat.transform.position
        ).normalized;
        Require(
            Vector3.Dot(
                context.FirstCombat.transform.forward,
                firstToSecond
            ) >= 0.999f &&
            Vector3.Dot(
                context.SecondCombat.transform.forward,
                -firstToSecond
            ) >= 0.999f,
            "Face strafe did not keep mutual orientation."
        );
        controller.ResetCameraView(true);
        RequirePairVisible(
            camera,
            context,
            "prolonged strafe"
        );
        Require(
            Quaternion.Angle(
                initialRotation,
                camera.transform.rotation
            ) >= 1f,
            "Camera did not rotate with the player during strafe."
        );
        RequireCameraBehindPlayer(
            camera,
            context,
            "prolonged strafe"
        );
        RequirePlayerScreenAnchor(
            camera,
            context,
            initialPlayerViewport,
            "prolonged strafe"
        );
        context.Controller.StopAllMovement();
        context.Controller.ResetDuel();
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static void RequirePairVisible(
        Camera camera,
        ValidationContext context,
        string situation)
    {
        RequireVisible(
            camera,
            context.FirstCombat.transform.position,
            $"Player is outside the frame at {situation}."
        );
        RequireVisible(
            camera,
            context.SecondCombat.transform.position,
            $"Opponent is outside the frame at {situation}."
        );
    }

    private static void RequireCameraBehindPlayer(
        Camera camera,
        ValidationContext context,
        string situation)
    {
        Vector3 duelForward = Vector3.ProjectOnPlane(
            context.SecondCombat.transform.position -
            context.FirstCombat.transform.position,
            Vector3.up
        ).normalized;
        Vector3 cameraForward = Vector3.ProjectOnPlane(
            camera.transform.forward,
            Vector3.up
        ).normalized;
        Vector3 playerToCamera = Vector3.ProjectOnPlane(
            camera.transform.position -
            context.FirstCombat.transform.position,
            Vector3.up
        ).normalized;

        Require(
            Vector3.Dot(cameraForward, duelForward) >= 0.995f,
            $"Camera does not face the opponent at {situation}."
        );
        Require(
            Vector3.Dot(playerToCamera, duelForward) <= -0.5f,
            $"Camera is not behind the player at {situation}."
        );
    }

    private static void RequirePlayerScreenAnchor(
        Camera camera,
        ValidationContext context,
        Vector3 expectedViewport,
        string situation)
    {
        Vector3 actualViewport =
            camera.WorldToViewportPoint(
                context.FirstCombat.transform.position
            );
        Require(
            Vector2.Distance(
                expectedViewport,
                actualViewport
            ) <= Tolerance,
            $"Player screen anchor drifted at {situation}. " +
            $"Expected=({expectedViewport.x:F3}, " +
            $"{expectedViewport.y:F3}), " +
            $"Actual=({actualViewport.x:F3}, " +
            $"{actualViewport.y:F3})."
        );
    }

    private static void RequireVisible(
        Camera camera,
        Vector3 worldPosition,
        string message)
    {
        Vector3 viewport =
            camera.WorldToViewportPoint(worldPosition);
        Require(
            viewport.z > 0f &&
            viewport.x >= 0f &&
            viewport.x <= 1f &&
            viewport.y >= 0f &&
            viewport.y <= 1f,
            message +
            $" Viewport=({viewport.x:F3}, " +
            $"{viewport.y:F3}, {viewport.z:F3})."
        );
    }

    private static void ValidateDistanceVisuals(
        ValidationContext context)
    {
        GameObject visualObject =
            ValidationContext.CreateHiddenObject(
                "V061 Validation Distance Visuals"
            );
        CombatDistanceDebugVisualizer visualizer =
            visualObject.AddComponent<
                CombatDistanceDebugVisualizer>();
        visualizer.Initialize(
            context.Controller,
            context.FirstCombat,
            context.SecondCombat
        );
        Require(
            visualObject.GetComponentsInChildren<Collider>(true).Length ==
                0,
            "Distance debug circles must not create colliders."
        );
        ValidateCircle(
            visualObject.transform,
            context,
            "Close Range Debug Circle",
            DistanceLevel.CloseRange
        );
        ValidateCircle(
            visualObject.transform,
            context,
            "Mid Range Debug Circle",
            DistanceLevel.MidRange
        );
        ValidateCircle(
            visualObject.transform,
            context,
            "Long Range Debug Circle",
            DistanceLevel.LongRange
        );
        ValidateRangeFill(
            visualObject.transform,
            context,
            "Close Range Debug Fill"
        );
        ValidateRangeFill(
            visualObject.transform,
            context,
            "Mid Range Debug Fill"
        );
        ValidateRangeFill(
            visualObject.transform,
            context,
            "Long Range Debug Fill"
        );
        Transform distanceRoot = visualObject.transform.Find(
            "Combat Distance Debug Root"
        );
        LineRenderer close = distanceRoot
            .Find("Close Range Debug Circle")
            .GetComponent<LineRenderer>();
        LineRenderer mid = distanceRoot
            .Find("Mid Range Debug Circle")
            .GetComponent<LineRenderer>();
        Require(
            mid.startWidth > close.startWidth,
            "The current MidRange circle is not highlighted."
        );
        Vector3 stableCirclePosition =
            close.transform.position;
        context.SecondCombat.transform.position +=
            Vector3.right * 0.5f;
        Invoke(visualizer, "LateUpdate");
        Require(
            Vector3.Distance(
                stableCirclePosition,
                close.transform.position
            ) <= Tolerance,
            "A temporary enemy animation moved the ground zones."
        );
        context.Controller.RestoreNeutralPose(
            context.SecondCombat
        );
        Require(
            context.Controller.TryPrepareDodge(
                context.SecondCombat,
                DodgeDirection.Left,
                out SpatialDodgeTransaction transaction
            ),
            "Unable to prepare the opponent dodge for distance visuals."
        );
        Require(
            context.Controller.PreviewPreparedDodge(
                transaction.Id,
                0.5f
            ),
            "Unable to preview the opponent dodge for distance visuals."
        );
        Invoke(visualizer, "LateUpdate");
        Vector2 rootPosition = new(
            distanceRoot.position.x,
            distanceRoot.position.z
        );
        Vector2 opponentPosition = new(
            context.SecondCombat.transform.position.x,
            context.SecondCombat.transform.position.z
        );
        Require(
            Vector2.Distance(
                rootPosition,
                opponentPosition
            ) <= Tolerance,
            "The ground zones did not follow the opponent dodge preview."
        );
        Require(
            context.Controller.CancelDodge(transaction),
            "Unable to cancel the opponent distance visual dodge."
        );
        Invoke(visualizer, "LateUpdate");
        visualizer.SetVisible(false);
        Require(
            !visualizer.IsVisible,
            "Distance debug visibility toggle failed."
        );
        visualizer.SetVisible(true);
        UnityEngine.Object.DestroyImmediate(visualObject);
    }

    private static void ValidateCircle(
        Transform visualRoot,
        ValidationContext context,
        string objectName,
        DistanceLevel level)
    {
        Transform distanceRoot = visualRoot.Find(
            "Combat Distance Debug Root"
        );
        Transform circleTransform =
            distanceRoot != null
                ? distanceRoot.Find(objectName)
                : null;
        Require(
            circleTransform != null,
            $"Missing distance circle {objectName}."
        );
        Require(
            circleTransform.parent == distanceRoot,
            $"{objectName} is not attached to the stable zone root."
        );
        LineRenderer line =
            circleTransform.GetComponent<LineRenderer>();
        Require(
            line != null && line.positionCount >= 24,
            $"{objectName} is not a reusable circle renderer."
        );
        Vector3 sample = line.GetPosition(0);
        RequireNear(
            new Vector2(sample.x, sample.z).magnitude,
            context.Controller.GetDistance(level),
            $"{objectName} does not use the spatial distance."
        );
    }

    private static void ValidateRangeFill(
        Transform visualRoot,
        ValidationContext context,
        string objectName)
    {
        Transform distanceRoot = visualRoot.Find(
            "Combat Distance Debug Root"
        );
        Transform fillTransform =
            distanceRoot != null
                ? distanceRoot.Find(objectName)
                : null;
        Require(
            fillTransform != null,
            $"Missing distance fill {objectName}."
        );
        Require(
            fillTransform.parent == distanceRoot,
            $"{objectName} is not attached to the stable zone root."
        );
        MeshFilter filter =
            fillTransform.GetComponent<MeshFilter>();
        MeshRenderer renderer =
            fillTransform.GetComponent<MeshRenderer>();
        Require(
            filter != null &&
            filter.sharedMesh != null &&
            filter.sharedMesh.vertexCount >= 25,
            $"{objectName} is not a filled mesh."
        );
        Require(
            renderer != null &&
            renderer.sharedMaterial != null &&
            renderer.sharedMaterial.color.a > 0f,
            $"{objectName} has no visible fill color."
        );
    }

    private static void CommitDodge(
        ValidationContext context,
        DodgeDirection direction,
        DistanceLevel expectedDistance)
    {
        Require(
            context.Controller.TryPrepareDodge(
                context.FirstCombat,
                direction,
                out SpatialDodgeTransaction transaction
            ),
            $"Unable to prepare {direction}."
        );
        Require(
            transaction.DistanceAfter == expectedDistance,
            $"{direction} prepared the wrong distance."
        );
        context.Controller.PreviewPreparedDodge(
            transaction.Id,
            1f
        );
        Require(
            context.Controller.CommitDodge(transaction),
            $"Unable to commit {direction}."
        );
        RequireDistance(
            context,
            expectedDistance,
            context.Controller.GetDistance(expectedDistance),
            $"{direction} did not finish on its exact anchor."
        );
    }

    private static List<TimedGestureSample> CreateStroke(
        params int[] zones)
    {
        List<TimedGestureSample> samples = new();
        float time = 0f;
        for (int zoneIndex = 0;
             zoneIndex < zones.Length;
             zoneIndex++)
        {
            Vector2 point =
                HybridGestureRecognizer.GetZoneCenter(
                    zones[zoneIndex],
                    0.1f
                );
            if (zoneIndex > 0)
            {
                Vector2 previous =
                    HybridGestureRecognizer.GetZoneCenter(
                        zones[zoneIndex - 1],
                        0.1f
                    );
                for (int step = 1; step <= 8; step++)
                {
                    time += 0.015f;
                    samples.Add(
                        new TimedGestureSample(
                            Vector2.Lerp(
                                previous,
                                point,
                                step / 8f
                            ),
                            time
                        )
                    );
                }
            }
            else
            {
                samples.Add(new TimedGestureSample(point, time));
            }
        }
        return samples;
    }

    private static void RequireDistance(
        ValidationContext context,
        DistanceLevel expectedLevel,
        float expectedDistance,
        string message)
    {
        Require(
            context.Controller.CurrentDistance == expectedLevel,
            message + " (logical level)"
        );
        RequireNear(
            context.Controller.Snapshot.Separation,
            expectedDistance,
            message + " (physical separation)"
        );
    }

    private static void RequireZones(
        IReadOnlyList<int> actual,
        params int[] expected)
    {
        Require(
            actual != null && actual.Count == expected.Length,
            "Gesture zone sequence length mismatch."
        );
        for (int index = 0; index < expected.Length; index++)
        {
            Require(
                actual[index] == expected[index],
                "Gesture zone sequence mismatch."
            );
        }
    }

    private static void Invoke(
        object target,
        string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Require(method != null, $"Missing method {methodName}.");
        method.Invoke(target, null);
    }

    private static void Invoke(
        object target,
        string methodName,
        object argument)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Require(method != null, $"Missing method {methodName}.");
        method.Invoke(target, new[] { argument });
    }

    private static void RequireNear(
        float actual,
        float expected,
        string message)
    {
        Require(
            Mathf.Abs(actual - expected) <= Tolerance,
            $"{message} Expected {expected}, got {actual}."
        );
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

        private ValidationContext(
            GameObject controllerRoot,
            GameObject firstRoot,
            GameObject secondRoot,
            CombatSpatialController controller,
            FighterCombat firstCombat,
            FighterCombat secondCombat,
            FighterStats firstStats)
        {
            controllerObject = controllerRoot;
            firstObject = firstRoot;
            secondObject = secondRoot;
            Controller = controller;
            FirstCombat = firstCombat;
            SecondCombat = secondCombat;
            FirstStats = firstStats;
        }

        public static ValidationContext Create()
        {
            GameObject first = CreateHiddenObject(
                "V061 Validation First"
            );
            GameObject second = CreateHiddenObject(
                "V061 Validation Second"
            );
            GameObject controllerRoot = CreateHiddenObject(
                "V061 Validation Spatial"
            );
            first.transform.position = new Vector3(0f, 1f, -3f);
            second.transform.position = new Vector3(0f, 1f, 3f);

            FighterStats firstStats =
                first.AddComponent<FighterStats>();
            FighterStats secondStats =
                second.AddComponent<FighterStats>();
            firstStats.ResetStats();
            secondStats.ResetStats();
            FighterCombat firstCombat =
                first.AddComponent<FighterCombat>();
            FighterCombat secondCombat =
                second.AddComponent<FighterCombat>();
            Wire(
                firstCombat,
                firstStats,
                secondStats,
                secondCombat
            );
            Wire(
                secondCombat,
                secondStats,
                firstStats,
                firstCombat
            );

            CombatSpatialController controller =
                controllerRoot.AddComponent<
                    CombatSpatialController>();
            controller.Configure(new CombatSpatialSettings
            {
                MinimumDistance = 3f,
                CloseRangeUpperBound = 4.25f,
                MidRangeDistance = 6f,
                MidRangeUpperBound = 7.25f,
                MaximumDistance = 9f,
                StrafeSpeed = 1.5f,
                RotationSpeed = 540f,
                DodgeOrientationAngle = 90f,
                AutoFaceFlanks = true,
                FlankAutoFaceDelay = 3f
            });
            Require(
                controller.Initialize(firstCombat, secondCombat),
                "Unable to initialize V061 validation duel."
            );
            firstCombat.SetSpatialController(controller);
            secondCombat.SetSpatialController(controller);

            return new ValidationContext(
                controllerRoot,
                first,
                second,
                controller,
                firstCombat,
                secondCombat,
                firstStats
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

        public static GameObject CreateHiddenObject(string name)
        {
            return new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void Wire(
            FighterCombat combat,
            FighterStats ownStats,
            FighterStats targetStats,
            FighterCombat targetCombat)
        {
            SerializedObject serialized = new(combat);
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
