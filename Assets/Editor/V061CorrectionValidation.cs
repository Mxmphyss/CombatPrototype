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
        Quaternion stableRotation = camera.transform.rotation;
        RequirePairVisible(camera, context, "MidRange");

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
                stableRotation,
                camera.transform.rotation
            ) <= 0.001f,
            "Automatic framing rotated the map."
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
            context,
            "Close Range Debug Circle",
            DistanceLevel.CloseRange
        );
        ValidateCircle(
            context,
            "Mid Range Debug Circle",
            DistanceLevel.MidRange
        );
        ValidateCircle(
            context,
            "Long Range Debug Circle",
            DistanceLevel.LongRange
        );
        LineRenderer close = context.SecondCombat.transform
            .Find("Close Range Debug Circle")
            .GetComponent<LineRenderer>();
        LineRenderer mid = context.SecondCombat.transform
            .Find("Mid Range Debug Circle")
            .GetComponent<LineRenderer>();
        Require(
            mid.startWidth > close.startWidth,
            "The current MidRange circle is not highlighted."
        );
        visualizer.SetVisible(false);
        Require(
            !visualizer.IsVisible,
            "Distance debug visibility toggle failed."
        );
        visualizer.SetVisible(true);
        UnityEngine.Object.DestroyImmediate(visualObject);
    }

    private static void ValidateCircle(
        ValidationContext context,
        string objectName,
        DistanceLevel level)
    {
        Transform circleTransform =
            context.SecondCombat.transform.Find(objectName);
        Require(
            circleTransform != null,
            $"Missing distance circle {objectName}."
        );
        Require(
            circleTransform.parent ==
                context.SecondCombat.transform,
            $"{objectName} does not follow the opponent."
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
