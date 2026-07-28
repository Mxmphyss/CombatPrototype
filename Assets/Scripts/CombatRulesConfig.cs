using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatRulesConfig",
    menuName = "Combat Prototype/Combat Rules")]
public sealed class CombatRulesConfig : ScriptableObject
{
    [Header("Garde maintenue")]
    [Min(0f)]
    [SerializeField] private float guardStaminaDamage = 5f;

    [Header("Garde brisee")]
    [Min(0.01f)]
    [SerializeField] private float guardBreakStunDuration = 4f;
    [Min(0f)]
    [SerializeField] private float stunRecoveryStamina = 15f;
    [SerializeField]
    private AnimationCurve stunRecoveryCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Feedback barre d'endurance")]
    [Range(0f, 1f)]
    [SerializeField]
    private float staminaBarFeedbackIntensity = 0.35f;
    [Min(0.01f)]
    [SerializeField]
    private float staminaBarFeedbackDuration = 0.18f;

    [Header("Feedback garde brisee")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float guardBreakCharacterFeedbackIntensity = 0.08f;
    [Min(0.01f)]
    [SerializeField]
    private float guardBreakCharacterFeedbackDuration = 0.55f;
    [SerializeField]
    private Color guardBreakFlashColor =
        new(1f, 0.38f, 0.16f, 1f);

    public float GuardStaminaDamage =>
        Mathf.Max(0f, guardStaminaDamage);
    public float GuardBreakStunDuration =>
        Mathf.Max(0.01f, guardBreakStunDuration);
    public float StunRecoveryStamina =>
        Mathf.Max(0f, stunRecoveryStamina);
    public AnimationCurve StunRecoveryCurve =>
        stunRecoveryCurve;
    public float StaminaBarFeedbackIntensity =>
        Mathf.Clamp01(staminaBarFeedbackIntensity);
    public float StaminaBarFeedbackDuration =>
        Mathf.Max(0.01f, staminaBarFeedbackDuration);
    public float GuardBreakCharacterFeedbackIntensity =>
        Mathf.Clamp(
            guardBreakCharacterFeedbackIntensity,
            0f,
            0.5f
        );
    public float GuardBreakCharacterFeedbackDuration =>
        Mathf.Max(
            0.01f,
            guardBreakCharacterFeedbackDuration
        );
    public Color GuardBreakFlashColor =>
        guardBreakFlashColor;

    private static CombatRulesConfig runtimeDefault;

    public static CombatRulesConfig RuntimeDefault
    {
        get
        {
            if (runtimeDefault != null)
                return runtimeDefault;

            runtimeDefault =
                CreateInstance<CombatRulesConfig>();
            runtimeDefault.name =
                "Runtime Default Combat Rules";
            runtimeDefault.hideFlags =
                HideFlags.HideAndDontSave;
            return runtimeDefault;
        }
    }

    public float ResolveGuardStaminaDamage(
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return ResolveFinalValue(
            GuardStaminaDamage,
            multiplier,
            additiveModifier
        );
    }

    public static float ResolveFinalValue(
        float baseValue,
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return Mathf.Max(
            0f,
            baseValue * Mathf.Max(0f, multiplier) +
            additiveModifier
        );
    }

    private void OnValidate()
    {
        guardStaminaDamage =
            Mathf.Max(0f, guardStaminaDamage);
        guardBreakStunDuration =
            Mathf.Max(0.01f, guardBreakStunDuration);
        stunRecoveryStamina =
            Mathf.Max(0f, stunRecoveryStamina);
        staminaBarFeedbackDuration =
            Mathf.Max(0.01f, staminaBarFeedbackDuration);
        guardBreakCharacterFeedbackDuration =
            Mathf.Max(
                0.01f,
                guardBreakCharacterFeedbackDuration
            );

        if (stunRecoveryCurve == null ||
            stunRecoveryCurve.length < 2)
        {
            stunRecoveryCurve =
                AnimationCurve.EaseInOut(
                    0f,
                    0f,
                    1f,
                    1f
                );
        }
    }
}
