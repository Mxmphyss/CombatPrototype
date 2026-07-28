using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatRulesConfig",
    menuName = "Combat Prototype/Combat Rules")]
public sealed class CombatRulesConfig : ScriptableObject
{
    private const float MinimumPositiveValue = 0.01f;

    [Header("Garde maintenue")]
    [Min(0f)]
    [SerializeField] private float guardStaminaDamage = 15f;

    [Header("Parade et riposte")]
    [Min(0f)]
    [SerializeField] private float riposteWindowDuration = 0.5f;

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

    [Header("Distances de combat")]
    [Min(MinimumPositiveValue)]
    [SerializeField] private float closeDistance = 3f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float midDistance = 6f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float longDistance = 9f;
    [Min(0f)]
    [SerializeField] private float distanceTolerance = 0.25f;

    [Header("Mouvement")]
    [Min(MinimumPositiveValue)]
    [SerializeField] private float forwardMoveSpeed = 2.5f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float backwardMoveSpeed = 2f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float lateralMoveSpeed = 1.5f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float rotationSpeed = 540f;
    [Min(0f)]
    [SerializeField] private float movementHoldDelay = 0.28f;

    [Header("Esquive spatiale")]
    [Min(MinimumPositiveValue)]
    [SerializeField] private float dodgeSpatialDuration = 0.28f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float dodgeSpatialSpeed = 12f;
    [Range(0f, 180f)]
    [SerializeField] private float dodgeOrientationAngle = 90f;

    [Header("Orientation et degats positionnels")]
    [Min(0f)]
    [SerializeField] private float flankAutoFaceDelay = 3f;
    [Min(0f)]
    [SerializeField] private float flankDamageMultiplier = 1.25f;
    [Min(0f)]
    [SerializeField] private float backDamageMultiplier = 2f;

    [Header("Permutation")]
    [Min(0f)]
    [SerializeField] private float permutationStaminaCost = 50f;
    [Min(MinimumPositiveValue)]
    [SerializeField] private float permutationFeedbackDuration = 0.35f;

    [Header("IA simple")]
    [SerializeField] private bool aiCompensationEnabled = true;
    [Range(0f, 1f)]
    [SerializeField] private float aiCompensationProbability = 0.65f;
    [Min(0f)]
    [SerializeField] private float aiCompensationMinDelay = 0.2f;
    [Min(0f)]
    [SerializeField] private float aiCompensationMaxDelay = 0.45f;
    [SerializeField] private bool aiPermutationEnabled;

    public float GuardStaminaDamage =>
        Mathf.Max(0f, guardStaminaDamage);
    public float RiposteWindowDuration =>
        Mathf.Max(0f, riposteWindowDuration);
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
    public float CloseDistance =>
        Mathf.Max(MinimumPositiveValue, closeDistance);
    public float MidDistance =>
        Mathf.Max(
            CloseDistance + MinimumPositiveValue,
            midDistance
        );
    public float LongDistance =>
        Mathf.Max(
            MidDistance + MinimumPositiveValue,
            longDistance
        );
    public float DistanceTolerance =>
        Mathf.Max(0f, distanceTolerance);
    public float ForwardMoveSpeed =>
        Mathf.Max(MinimumPositiveValue, forwardMoveSpeed);
    public float BackwardMoveSpeed =>
        Mathf.Max(MinimumPositiveValue, backwardMoveSpeed);
    public float LateralMoveSpeed =>
        Mathf.Max(MinimumPositiveValue, lateralMoveSpeed);
    public float RotationSpeed =>
        Mathf.Max(MinimumPositiveValue, rotationSpeed);
    public float MovementHoldDelay =>
        Mathf.Max(0f, movementHoldDelay);
    public float DodgeSpatialDuration =>
        Mathf.Max(MinimumPositiveValue, dodgeSpatialDuration);
    public float DodgeSpatialSpeed =>
        Mathf.Max(MinimumPositiveValue, dodgeSpatialSpeed);
    public float DodgeOrientationAngle =>
        Mathf.Clamp(dodgeOrientationAngle, 0f, 180f);
    public float FlankAutoFaceDelay =>
        Mathf.Max(0f, flankAutoFaceDelay);
    public float FlankDamageMultiplier =>
        Mathf.Max(0f, flankDamageMultiplier);
    public float BackDamageMultiplier =>
        Mathf.Max(0f, backDamageMultiplier);
    public float PermutationStaminaCost =>
        Mathf.Max(0f, permutationStaminaCost);
    public float PermutationFeedbackDuration =>
        Mathf.Max(
            MinimumPositiveValue,
            permutationFeedbackDuration
        );
    public bool AiCompensationEnabled =>
        aiCompensationEnabled;
    public float AiCompensationProbability =>
        Mathf.Clamp01(aiCompensationProbability);
    public float AiCompensationMinDelay =>
        Mathf.Max(0f, aiCompensationMinDelay);
    public float AiCompensationMaxDelay =>
        Mathf.Max(
            AiCompensationMinDelay,
            aiCompensationMaxDelay
        );
    public bool AiPermutationEnabled =>
        aiPermutationEnabled;

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

    public float ResolveFlankDamage(
        float baseDamage,
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return ResolvePositionalDamage(
            baseDamage,
            FlankDamageMultiplier,
            multiplier,
            additiveModifier
        );
    }

    public float ResolveBackDamage(
        float baseDamage,
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return ResolvePositionalDamage(
            baseDamage,
            BackDamageMultiplier,
            multiplier,
            additiveModifier
        );
    }

    public float ResolvePermutationStaminaCost(
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return ResolveFinalValue(
            PermutationStaminaCost,
            multiplier,
            additiveModifier
        );
    }

    public static float ResolvePositionalDamage(
        float baseDamage,
        float positionalMultiplier,
        float multiplier = 1f,
        float additiveModifier = 0f)
    {
        return ResolveFinalValue(
            baseDamage * Mathf.Max(0f, positionalMultiplier),
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
        riposteWindowDuration =
            Mathf.Max(0f, riposteWindowDuration);
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
        closeDistance =
            Mathf.Max(MinimumPositiveValue, closeDistance);
        midDistance =
            Mathf.Max(
                closeDistance + MinimumPositiveValue,
                midDistance
            );
        longDistance =
            Mathf.Max(
                midDistance + MinimumPositiveValue,
                longDistance
            );
        distanceTolerance =
            Mathf.Max(0f, distanceTolerance);
        forwardMoveSpeed =
            Mathf.Max(
                MinimumPositiveValue,
                forwardMoveSpeed
            );
        backwardMoveSpeed =
            Mathf.Max(
                MinimumPositiveValue,
                backwardMoveSpeed
            );
        lateralMoveSpeed =
            Mathf.Max(
                MinimumPositiveValue,
                lateralMoveSpeed
            );
        rotationSpeed =
            Mathf.Max(MinimumPositiveValue, rotationSpeed);
        movementHoldDelay =
            Mathf.Max(0f, movementHoldDelay);
        dodgeSpatialDuration =
            Mathf.Max(
                MinimumPositiveValue,
                dodgeSpatialDuration
            );
        dodgeSpatialSpeed =
            Mathf.Max(
                MinimumPositiveValue,
                dodgeSpatialSpeed
            );
        dodgeOrientationAngle =
            Mathf.Clamp(dodgeOrientationAngle, 0f, 180f);
        flankAutoFaceDelay =
            Mathf.Max(0f, flankAutoFaceDelay);
        flankDamageMultiplier =
            Mathf.Max(0f, flankDamageMultiplier);
        backDamageMultiplier =
            Mathf.Max(0f, backDamageMultiplier);
        permutationStaminaCost =
            Mathf.Max(0f, permutationStaminaCost);
        permutationFeedbackDuration =
            Mathf.Max(
                MinimumPositiveValue,
                permutationFeedbackDuration
            );
        aiCompensationProbability =
            Mathf.Clamp01(aiCompensationProbability);
        aiCompensationMinDelay =
            Mathf.Max(0f, aiCompensationMinDelay);
        aiCompensationMaxDelay =
            Mathf.Max(
                aiCompensationMinDelay,
                aiCompensationMaxDelay
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
