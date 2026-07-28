using System.Collections;
using UnityEngine;

public sealed class CombatFeedbackEffects : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool feedbackEnabled = true;
    [SerializeField] private bool hitStopEnabled = true;
    [SerializeField] private bool cameraShakeEnabled = true;
    [SerializeField] private bool flashEnabled = true;
    [SerializeField] private bool recoilEnabled = true;

    [Header("Impact")]
    [Min(0f)]
    [SerializeField] private float recoilDistance = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float recoilDuration = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.09f;
    [Min(0f)]
    [SerializeField] private float hitStopDuration = 0.045f;
    [Range(0f, 1f)]
    [SerializeField] private float hitStopTimeScale = 0.08f;

    [Header("Camera")]
    [Min(0f)]
    [SerializeField] private float cameraShakeDuration = 0.1f;
    [Min(0f)]
    [SerializeField] private float cameraShakeStrength = 0.025f;

    private FighterCombat player;
    private FighterCombat enemy;
    private CombatSpatialController spatialController;
    private CombatCameraController cameraController;
    private Camera combatCamera;
    private Renderer playerRenderer;
    private Renderer enemyRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 cameraStartPosition;
    private Vector3 playerNeutralPosition;
    private Vector3 enemyNeutralPosition;
    private Vector3 playerNeutralScale;
    private Vector3 enemyNeutralScale;
    private Coroutine hitStopRoutine;
    private Coroutine cameraShakeRoutine;
    private Coroutine playerFlashRoutine;
    private Coroutine enemyFlashRoutine;
    private Coroutine playerRecoilRoutine;
    private Coroutine enemyRecoilRoutine;
    private Coroutine playerGuardBreakRoutine;
    private Coroutine enemyGuardBreakRoutine;
    private bool initialized;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    public void Initialize(
        FighterCombat playerCombat,
        FighterCombat enemyCombat,
        Camera targetCamera,
        CombatSpatialController spatialAuthority = null,
        CombatCameraController cameraAuthority = null)
    {
        Unsubscribe();
        ResetEffects();

        player = playerCombat;
        enemy = enemyCombat;
        spatialController = spatialAuthority;
        cameraController = cameraAuthority;
        combatCamera = targetCamera;
        playerRenderer = player.GetComponentInChildren<Renderer>();
        enemyRenderer = enemy.GetComponentInChildren<Renderer>();
        propertyBlock ??= new MaterialPropertyBlock();

        playerNeutralPosition = player.transform.position;
        enemyNeutralPosition = enemy.transform.position;
        playerNeutralScale = player.transform.localScale;
        enemyNeutralScale = enemy.transform.localScale;
        if (combatCamera != null)
            cameraStartPosition = combatCamera.transform.localPosition;

        Subscribe();
        initialized = true;
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetEffects();
    }

    public void ResetEffects()
    {
        StopAllCoroutines();

        if (initialized)
        {
            if (player != null)
            {
                if (spatialController == null)
                    player.transform.position = playerNeutralPosition;
                player.transform.localScale = playerNeutralScale;
            }
            if (enemy != null)
            {
                if (spatialController == null)
                    enemy.transform.position = enemyNeutralPosition;
                enemy.transform.localScale = enemyNeutralScale;
            }
            spatialController?.RestoreNeutralPoses();
            cameraController?.SetShakeOffset(Vector3.zero);
            if (cameraController == null &&
                combatCamera != null)
            {
                combatCamera.transform.localPosition =
                    cameraStartPosition;
            }
        }

        ClearFlash(playerRenderer);
        ClearFlash(enemyRenderer);
        Time.timeScale = 1f;

        hitStopRoutine = null;
        cameraShakeRoutine = null;
        playerFlashRoutine = null;
        enemyFlashRoutine = null;
        playerRecoilRoutine = null;
        enemyRecoilRoutine = null;
        playerGuardBreakRoutine = null;
        enemyGuardBreakRoutine = null;
    }

    private void Subscribe()
    {
        if (player != null)
        {
            player.OnAttackResolved += HandleImpact;
            player.OnGuardImpact += HandleGuardImpact;
        }
        if (enemy != null)
        {
            enemy.OnAttackResolved += HandleImpact;
            enemy.OnGuardImpact += HandleGuardImpact;
        }
    }

    private void Unsubscribe()
    {
        if (player != null)
        {
            player.OnAttackResolved -= HandleImpact;
            player.OnGuardImpact -= HandleGuardImpact;
        }
        if (enemy != null)
        {
            enemy.OnAttackResolved -= HandleImpact;
            enemy.OnGuardImpact -= HandleGuardImpact;
        }
    }

    private void HandleImpact(CombatImpact impact)
    {
        if (!feedbackEnabled ||
            (spatialController != null &&
             !spatialController.IsCombatEnabled))
            return;

        bool directHit = impact.Result == CombatHitResult.Hit;
        bool perfect =
            impact.Result == CombatHitResult.PerfectGuard ||
            impact.Result == CombatHitResult.PerfectDodge;

        if (directHit)
        {
            PlayTargetFeedback(impact.Target, impact.Attacker);
            PlayHitStop(hitStopDuration);
            PlayCameraShake(1f);
        }
        else if (perfect)
        {
            PlayTargetFlash(impact.Attacker);
            PlayHitStop(hitStopDuration * 1.5f);
            PlayCameraShake(0.65f);
        }
    }

    private void HandleGuardImpact(GuardImpact impact)
    {
        if (!feedbackEnabled ||
            !impact.GuardBroken ||
            impact.Target == null)
        {
            return;
        }

        CombatRulesConfig rules = impact.Target.Rules;
        PlayTargetFlash(
            impact.Target,
            rules.GuardBreakFlashColor,
            rules.GuardBreakCharacterFeedbackDuration
        );
        PlayGuardBreakPulse(
            impact.Target,
            rules.GuardBreakCharacterFeedbackIntensity,
            rules.GuardBreakCharacterFeedbackDuration
        );
    }

    private void PlayTargetFeedback(
        FighterCombat target,
        FighterCombat attacker)
    {
        if (target == null)
            return;

        PlayTargetFlash(target);

        if (!recoilEnabled)
            return;

        Vector3 away = target.transform.position -
            attacker.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.0001f)
            away = -target.transform.forward;

        Coroutine existing =
            target == player ? playerRecoilRoutine : enemyRecoilRoutine;
        if (existing != null)
            StopCoroutine(existing);

        Coroutine routine = StartCoroutine(
            RecoilRoutine(
                target,
                away.normalized
            )
        );
        if (target == player)
            playerRecoilRoutine = routine;
        else
            enemyRecoilRoutine = routine;
    }

    private void PlayTargetFlash(FighterCombat target)
    {
        PlayTargetFlash(
            target,
            Color.white,
            flashDuration
        );
    }

    private void PlayTargetFlash(
        FighterCombat target,
        Color color,
        float duration)
    {
        if (!flashEnabled || target == null)
            return;

        Renderer targetRenderer =
            target == player ? playerRenderer : enemyRenderer;
        if (targetRenderer == null)
            return;

        Coroutine existing =
            target == player ? playerFlashRoutine : enemyFlashRoutine;
        if (existing != null)
            StopCoroutine(existing);

        Coroutine routine = StartCoroutine(
            FlashRoutine(
                targetRenderer,
                color,
                duration
            )
        );
        if (target == player)
            playerFlashRoutine = routine;
        else
            enemyFlashRoutine = routine;
    }

    private void PlayGuardBreakPulse(
        FighterCombat target,
        float intensity,
        float duration)
    {
        Coroutine existing = target == player
            ? playerGuardBreakRoutine
            : enemyGuardBreakRoutine;
        if (existing != null)
            StopCoroutine(existing);

        Vector3 neutralScale = target == player
            ? playerNeutralScale
            : enemyNeutralScale;
        Coroutine routine = StartCoroutine(
            GuardBreakPulseRoutine(
                target,
                neutralScale,
                intensity,
                duration
            )
        );

        if (target == player)
            playerGuardBreakRoutine = routine;
        else
            enemyGuardBreakRoutine = routine;
    }

    private IEnumerator GuardBreakPulseRoutine(
        FighterCombat target,
        Vector3 neutralScale,
        float intensity,
        float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeIntensity = Mathf.Clamp(
            intensity,
            0f,
            0.5f
        );
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized =
                Mathf.Clamp01(elapsed / safeDuration);
            float pulse =
                Mathf.Sin(normalized * Mathf.PI * 3f) *
                (1f - normalized);
            target.transform.localScale =
                neutralScale *
                (1f + pulse * safeIntensity);
            yield return null;
        }

        target.transform.localScale = neutralScale;
        if (target == player)
            playerGuardBreakRoutine = null;
        else
            enemyGuardBreakRoutine = null;
    }

    private void PlayHitStop(float duration)
    {
        if (!hitStopEnabled || duration <= 0f)
            return;

        if (hitStopRoutine != null)
            StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private void PlayCameraShake(float multiplier)
    {
        if (!cameraShakeEnabled ||
            combatCamera == null ||
            cameraShakeDuration <= 0f ||
            cameraShakeStrength <= 0f)
        {
            return;
        }

        if (cameraShakeRoutine != null)
            StopCoroutine(cameraShakeRoutine);
        cameraShakeRoutine = StartCoroutine(
            CameraShakeRoutine(multiplier)
        );
    }

    private IEnumerator RecoilRoutine(
        FighterCombat target,
        Vector3 direction)
    {
        Vector3 neutralPosition =
            GetCurrentNeutralPosition(target);
        Vector3 startOffset =
            target.transform.position - neutralPosition;
        Vector3 peakOffset =
            direction * recoilDistance;
        float halfDuration =
            Mathf.Max(0.005f, recoilDuration * 0.5f);
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            neutralPosition =
                GetCurrentNeutralPosition(target);
            target.transform.position =
                neutralPosition +
                Vector3.Lerp(
                    startOffset,
                    peakOffset,
                    Mathf.Clamp01(elapsed / halfDuration)
                );
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            neutralPosition =
                GetCurrentNeutralPosition(target);
            target.transform.position =
                neutralPosition +
                Vector3.Lerp(
                    peakOffset,
                    Vector3.zero,
                    Mathf.Clamp01(elapsed / halfDuration)
                );
            yield return null;
        }

        if (spatialController != null)
            spatialController.RestoreNeutralPose(target);
        else
            target.transform.position =
                GetCurrentNeutralPosition(target);
        if (target == player)
            playerRecoilRoutine = null;
        else
            enemyRecoilRoutine = null;
    }

    private Vector3 GetCurrentNeutralPosition(
        FighterCombat target)
    {
        if (spatialController != null &&
            spatialController.TryGetNeutralPosition(
                target,
                out Vector3 neutralPosition
            ))
        {
            return neutralPosition;
        }

        return target == player
            ? playerNeutralPosition
            : enemyNeutralPosition;
    }

    private IEnumerator FlashRoutine(
        Renderer targetRenderer,
        Color color,
        float duration)
    {
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(propertyBlock);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ClearFlash(targetRenderer);
        if (targetRenderer == playerRenderer)
            playerFlashRoutine = null;
        else
            enemyFlashRoutine = null;
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = Mathf.Clamp(hitStopTimeScale, 0.01f, 1f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    private IEnumerator CameraShakeRoutine(float multiplier)
    {
        float elapsed = 0f;
        while (elapsed < cameraShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Vector2 offset =
                Random.insideUnitCircle *
                cameraShakeStrength *
                multiplier;
            Vector3 shake =
                new(offset.x, offset.y, 0f);
            if (cameraController != null)
                cameraController.SetShakeOffset(shake);
            else
                combatCamera.transform.localPosition =
                    cameraStartPosition + shake;
            yield return null;
        }

        if (cameraController != null)
            cameraController.SetShakeOffset(Vector3.zero);
        else
            combatCamera.transform.localPosition =
                cameraStartPosition;
        cameraShakeRoutine = null;
    }

    private static IEnumerator MoveUnscaled(
        Transform target,
        Vector3 from,
        Vector3 to,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            target.position = Vector3.Lerp(
                from,
                to,
                Mathf.Clamp01(elapsed / duration)
            );
            yield return null;
        }
    }

    private void ClearFlash(Renderer targetRenderer)
    {
        if (targetRenderer == null || propertyBlock == null)
            return;

        targetRenderer.SetPropertyBlock(null);
    }
}
