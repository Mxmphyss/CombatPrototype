using System;
using UnityEngine;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

[DisallowMultipleComponent]
public sealed class CombatCameraController : MonoBehaviour
{
    [Header("Cadrage automatique")]
    [Range(0f, 0.45f)]
    [SerializeField] private float opponentFramingBias = 0.18f;
    [Range(0.2f, 0.75f)]
    [SerializeField] private float maximumOpponentFramingBias = 0.65f;
    [Range(0.01f, 0.2f)]
    [SerializeField] private float framingBiasStep = 0.05f;
    [Min(0f)]
    [SerializeField] private float worldFramingMargin = 0.8f;
    [Min(0.01f)]
    [SerializeField] private float followSmoothTime = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float zoomSmoothTime = 0.12f;

    [Header("Zoom")]
    [SerializeField] private float minimumZoom = 28f;
    [SerializeField] private float maximumZoom = 72f;
    [Min(0f)]
    [SerializeField] private float pinchZoomSensitivity = 36f;

    [Header("Panoramique")]
    [Min(0f)]
    [SerializeField] private float panSensitivity = 7f;
    [Range(-1f, 1f)]
    [SerializeField] private float sameDirectionThreshold = 0.35f;

    private Camera combatCamera;
    private FighterCombat player;
    private FighterCombat opponent;
    private Quaternion neutralCameraLocalRotation;
    private Vector3 neutralPlayerOffset;
    private Vector3 manualPanOffset;
    private Vector3 shakeOffset;
    private Vector3 followVelocity;
    private float neutralZoom;
    private float manualZoomOffset;
    private float zoomVelocity;
    private bool initialized;
    private bool multiTouchActive;

    public event Action<bool> OnMultiTouchStateChanged;

    public bool IsMultiTouchActive => multiTouchActive;
    public bool IsManualViewActive =>
        manualPanOffset.sqrMagnitude > 0.000001f ||
        Mathf.Abs(manualZoomOffset) > 0.001f;
    public float CurrentZoom =>
        combatCamera == null
            ? 0f
            : combatCamera.orthographic
                ? combatCamera.orthographicSize
                : combatCamera.fieldOfView;

    public void Initialize(
        Camera targetCamera,
        FighterCombat playerFighter,
        FighterCombat opponentFighter,
        CombatSpatialController spatialAuthority)
    {
        combatCamera = targetCamera;
        player = playerFighter;
        opponent = opponentFighter;

        if (combatCamera == null || player == null)
        {
            enabled = false;
            return;
        }

        Quaternion initialDuelRotation =
            CalculateDuelFrameRotation();
        neutralCameraLocalRotation =
            Quaternion.Inverse(initialDuelRotation) *
            combatCamera.transform.rotation;
        neutralPlayerOffset =
            Quaternion.Inverse(initialDuelRotation) *
            (
                combatCamera.transform.position -
                player.transform.position
            );
        neutralZoom = combatCamera.orthographic
            ? combatCamera.orthographicSize
            : combatCamera.fieldOfView;
        minimumZoom = Mathf.Min(minimumZoom, maximumZoom);
        maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
        initialized = true;
        ResetCameraView(true);
    }

    private void OnEnable()
    {
        EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        SetMultiTouchActive(false);
        manualPanOffset = Vector3.zero;
        manualZoomOffset = 0f;
        shakeOffset = Vector3.zero;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelTransientInput();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelTransientInput();
    }

    private void LateUpdate()
    {
        if (!initialized || combatCamera == null || player == null)
            return;

        UpdateTwoFingerInput();
        UpdateAutomaticFraming(Time.unscaledDeltaTime);
    }

    public void ResetCameraView(bool immediate = true)
    {
        manualPanOffset = Vector3.zero;
        manualZoomOffset = 0f;
        shakeOffset = Vector3.zero;
        followVelocity = Vector3.zero;
        zoomVelocity = 0f;
        CancelTransientInput();

        if (!initialized || combatCamera == null || player == null)
            return;

        if (immediate)
            ApplyAutomaticFramingImmediate();
    }

    public void CancelTransientInput()
    {
        SetMultiTouchActive(false);
    }

    public void SetShakeOffset(Vector3 offset)
    {
        shakeOffset = offset;
    }

    internal void ApplyManualPanDelta(Vector2 normalizedDelta)
    {
        if (combatCamera == null)
            return;

        Vector3 right = combatCamera.transform.right;
        Vector3 up = combatCamera.transform.up;
        manualPanOffset +=
            (-right * normalizedDelta.x -
             up * normalizedDelta.y) * panSensitivity;
    }

    internal void ApplyPinchDelta(float normalizedDelta)
    {
        manualZoomOffset = Mathf.Clamp(
            manualZoomOffset -
            normalizedDelta * pinchZoomSensitivity,
            minimumZoom - neutralZoom,
            maximumZoom - neutralZoom
        );
    }

    private void UpdateTwoFingerInput()
    {
        var touches = EnhancedTouch.Touch.activeTouches;
        if (touches.Count < 2)
        {
            SetMultiTouchActive(false);
            return;
        }

        SetMultiTouchActive(true);
        EnhancedTouch.Touch first = touches[0];
        EnhancedTouch.Touch second = touches[1];
        Vector2 firstDelta = first.delta;
        Vector2 secondDelta = second.delta;
        float screenScale = Mathf.Max(
            1f,
            Mathf.Min(Screen.width, Screen.height)
        );

        Vector2 previousFirst =
            first.screenPosition - firstDelta;
        Vector2 previousSecond =
            second.screenPosition - secondDelta;
        float previousDistance =
            Vector2.Distance(previousFirst, previousSecond);
        float currentDistance =
            Vector2.Distance(
                first.screenPosition,
                second.screenPosition
            );
        ApplyPinchDelta(
            (currentDistance - previousDistance) /
            screenScale
        );

        float directionAgreement =
            firstDelta.sqrMagnitude <= Mathf.Epsilon ||
            secondDelta.sqrMagnitude <= Mathf.Epsilon
                ? 1f
                : Vector2.Dot(
                    firstDelta.normalized,
                    secondDelta.normalized
                );
        if (directionAgreement >= sameDirectionThreshold)
        {
            ApplyManualPanDelta(
                (firstDelta + secondDelta) *
                0.5f /
                screenScale
            );
        }
    }

    private void UpdateAutomaticFraming(float deltaTime)
    {
        Vector3 targetPosition = CalculateTargetPosition();
        Quaternion targetRotation = CalculateTargetRotation();
        combatCamera.transform.position = Vector3.SmoothDamp(
            combatCamera.transform.position,
            targetPosition + shakeOffset,
            ref followVelocity,
            followSmoothTime,
            Mathf.Infinity,
            Mathf.Max(0.0001f, deltaTime)
        );
        combatCamera.transform.rotation = targetRotation;

        float targetZoom = CalculateTargetZoom(
            targetPosition,
            targetRotation
        );
        if (combatCamera.orthographic)
        {
            combatCamera.orthographicSize = Mathf.SmoothDamp(
                combatCamera.orthographicSize,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime)
            );
        }
        else
        {
            combatCamera.fieldOfView = Mathf.SmoothDamp(
                combatCamera.fieldOfView,
                targetZoom,
                ref zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime)
            );
        }
    }

    private void ApplyAutomaticFramingImmediate()
    {
        Vector3 targetPosition = CalculateTargetPosition();
        Quaternion targetRotation = CalculateTargetRotation();
        combatCamera.transform.SetPositionAndRotation(
            targetPosition,
            targetRotation
        );
        float targetZoom = CalculateTargetZoom(
            targetPosition,
            targetRotation
        );
        if (combatCamera.orthographic)
            combatCamera.orthographicSize = targetZoom;
        else
            combatCamera.fieldOfView = targetZoom;
    }

    private Vector3 CalculateTargetPosition()
    {
        Vector3 playerPosition = GetNeutralPosition(player);
        if (opponent == null)
        {
            return playerPosition +
                CalculateDuelFrameRotation() *
                neutralPlayerOffset +
                manualPanOffset;
        }

        Vector3 opponentPosition = GetNeutralPosition(opponent);
        float bias = Mathf.Clamp(
            opponentFramingBias,
            0f,
            maximumOpponentFramingBias
        );
        Vector3 automaticPosition =
            CalculatePositionForBias(
                playerPosition,
                opponentPosition,
                bias
            );
        float maximumBias = Mathf.Max(
            bias,
            maximumOpponentFramingBias
        );
        float safeStep = Mathf.Max(0.01f, framingBiasStep);
        Quaternion targetRotation = CalculateTargetRotation();
        while (bias + 0.0001f < maximumBias &&
               RequiredAutomaticZoom(
                   automaticPosition,
                   targetRotation
               ) >
                   maximumZoom)
        {
            bias = Mathf.Min(maximumBias, bias + safeStep);
            automaticPosition = CalculatePositionForBias(
                playerPosition,
                opponentPosition,
                bias
            );
        }

        return automaticPosition + manualPanOffset;
    }

    private Vector3 CalculatePositionForBias(
        Vector3 playerPosition,
        Vector3 opponentPosition,
        float bias)
    {
        return Vector3.Lerp(
            playerPosition,
            opponentPosition,
            bias
        ) + CalculateDuelFrameRotation() * neutralPlayerOffset;
    }

    private float RequiredAutomaticZoom(
        Vector3 cameraPosition,
        Quaternion cameraRotation)
    {
        return combatCamera.orthographic
            ? CalculateRequiredOrthographicSize(
                cameraPosition,
                cameraRotation
            )
            : CalculateRequiredPerspectiveFov(
                cameraPosition,
                cameraRotation
            );
    }

    private float CalculateTargetZoom(
        Vector3 cameraPosition,
        Quaternion cameraRotation)
    {
        float automaticZoom = neutralZoom;
        if (opponent != null)
        {
            automaticZoom =
                RequiredAutomaticZoom(
                    cameraPosition,
                    cameraRotation
                );
            automaticZoom = Mathf.Max(neutralZoom, automaticZoom);
        }

        return Mathf.Clamp(
            automaticZoom + manualZoomOffset,
            minimumZoom,
            maximumZoom
        );
    }

    private float CalculateRequiredPerspectiveFov(
        Vector3 cameraPosition,
        Quaternion cameraRotation)
    {
        Quaternion inverseRotation =
            Quaternion.Inverse(cameraRotation);
        float playerFov = RequiredVerticalFov(
            inverseRotation *
            (GetNeutralPosition(player) - cameraPosition)
        );
        float opponentFov = RequiredVerticalFov(
            inverseRotation *
            (GetNeutralPosition(opponent) - cameraPosition)
        );
        return Mathf.Max(playerFov, opponentFov);
    }

    private float RequiredVerticalFov(Vector3 localPosition)
    {
        if (localPosition.z <= 0.01f)
            return maximumZoom;

        float vertical = 2f * Mathf.Atan2(
            Mathf.Abs(localPosition.y) + worldFramingMargin,
            localPosition.z
        ) * Mathf.Rad2Deg;
        float horizontal = 2f * Mathf.Atan2(
            Mathf.Abs(localPosition.x) + worldFramingMargin,
            localPosition.z
        );
        float horizontalAsVertical = 2f * Mathf.Atan(
            Mathf.Tan(horizontal * 0.5f) /
            Mathf.Max(0.1f, combatCamera.aspect)
        ) * Mathf.Rad2Deg;
        return Mathf.Max(vertical, horizontalAsVertical);
    }

    private float CalculateRequiredOrthographicSize(
        Vector3 cameraPosition,
        Quaternion cameraRotation)
    {
        Quaternion inverseRotation =
            Quaternion.Inverse(cameraRotation);
        Vector3 playerLocal =
            inverseRotation *
            (GetNeutralPosition(player) - cameraPosition);
        Vector3 opponentLocal =
            inverseRotation *
            (GetNeutralPosition(opponent) - cameraPosition);
        float vertical = Mathf.Max(
            Mathf.Abs(playerLocal.y),
            Mathf.Abs(opponentLocal.y)
        ) + worldFramingMargin;
        float horizontal = (
            Mathf.Max(
                Mathf.Abs(playerLocal.x),
                Mathf.Abs(opponentLocal.x)
            ) + worldFramingMargin
        ) / Mathf.Max(0.1f, combatCamera.aspect);
        return Mathf.Max(vertical, horizontal);
    }

    private Vector3 GetNeutralPosition(FighterCombat fighter)
    {
        return fighter != null
            ? fighter.transform.position
            : Vector3.zero;
    }

    private Quaternion CalculateTargetRotation()
    {
        return CalculateDuelFrameRotation() *
            neutralCameraLocalRotation;
    }

    private Quaternion CalculateDuelFrameRotation()
    {
        Vector3 duelDirection =
            opponent != null
                ? Horizontal(
                    opponent.transform.position -
                    player.transform.position
                )
                : Horizontal(player.transform.forward);
        if (duelDirection.sqrMagnitude <= 0.000001f)
            duelDirection = Vector3.forward;

        return Quaternion.LookRotation(
            duelDirection.normalized,
            Vector3.up
        );
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private void SetMultiTouchActive(bool active)
    {
        if (multiTouchActive == active)
            return;

        multiTouchActive = active;
        OnMultiTouchStateChanged?.Invoke(active);
    }
}
