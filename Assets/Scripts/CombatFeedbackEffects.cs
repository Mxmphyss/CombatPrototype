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
    private Camera combatCamera;
    private Renderer playerRenderer;
    private Renderer enemyRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 cameraStartPosition;
    private Vector3 playerNeutralPosition;
    private Vector3 enemyNeutralPosition;
    private Coroutine hitStopRoutine;
    private Coroutine cameraShakeRoutine;
    private Coroutine playerFlashRoutine;
    private Coroutine enemyFlashRoutine;
    private Coroutine playerRecoilRoutine;
    private Coroutine enemyRecoilRoutine;
    private bool initialized;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    public void Initialize(
        FighterCombat playerCombat,
        FighterCombat enemyCombat,
        Camera targetCamera)
    {
        Unsubscribe();
        ResetEffects();

        player = playerCombat;
        enemy = enemyCombat;
        combatCamera = targetCamera;
        playerRenderer = player.GetComponentInChildren<Renderer>();
        enemyRenderer = enemy.GetComponentInChildren<Renderer>();
        propertyBlock ??= new MaterialPropertyBlock();

        playerNeutralPosition = player.transform.position;
        enemyNeutralPosition = enemy.transform.position;
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
                player.transform.position = playerNeutralPosition;
            if (enemy != null)
                enemy.transform.position = enemyNeutralPosition;
            if (combatCamera != null)
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
    }

    private void Subscribe()
    {
        if (player != null)
            player.OnAttackResolved += HandleImpact;
        if (enemy != null)
            enemy.OnAttackResolved += HandleImpact;
    }

    private void Unsubscribe()
    {
        if (player != null)
            player.OnAttackResolved -= HandleImpact;
        if (enemy != null)
            enemy.OnAttackResolved -= HandleImpact;
    }

    private void HandleImpact(CombatImpact impact)
    {
        if (!feedbackEnabled)
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

        Vector3 neutralPosition =
            target == player
                ? playerNeutralPosition
                : enemyNeutralPosition;
        Coroutine routine = StartCoroutine(
            RecoilRoutine(
                target,
                away.normalized,
                neutralPosition
            )
        );
        if (target == player)
            playerRecoilRoutine = routine;
        else
            enemyRecoilRoutine = routine;
    }

    private void PlayTargetFlash(FighterCombat target)
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
            FlashRoutine(targetRenderer)
        );
        if (target == player)
            playerFlashRoutine = routine;
        else
            enemyFlashRoutine = routine;
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
        Vector3 direction,
        Vector3 neutralPosition)
    {
        Vector3 start = target.transform.position;
        Vector3 peak = start + direction * recoilDistance;
        float halfDuration =
            Mathf.Max(0.005f, recoilDuration * 0.5f);

        yield return MoveUnscaled(
            target.transform,
            start,
            peak,
            halfDuration
        );
        yield return MoveUnscaled(
            target.transform,
            peak,
            neutralPosition,
            halfDuration
        );

        target.transform.position = neutralPosition;
        if (target == player)
            playerRecoilRoutine = null;
        else
            enemyRecoilRoutine = null;
    }

    private IEnumerator FlashRoutine(Renderer targetRenderer)
    {
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, Color.white);
        propertyBlock.SetColor(ColorId, Color.white);
        targetRenderer.SetPropertyBlock(propertyBlock);

        float elapsed = 0f;
        while (elapsed < flashDuration)
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
            combatCamera.transform.localPosition =
                cameraStartPosition +
                new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

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
