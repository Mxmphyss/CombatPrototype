public readonly struct RoutedGestureAction
{
    public bool IsMapped { get; }
    public bool HasCombatResult { get; }
    public CombatActionResult CombatResult { get; }
    public CombatRefusalReason RefusalReason { get; }
    public string DisplayName { get; }
    public string Label => DisplayName;
    public int CategoryZone { get; }

    public RoutedGestureAction(
        bool isMapped,
        bool hasCombatResult,
        CombatActionResult combatResult,
        string displayName,
        int categoryZone,
        CombatRefusalReason refusalReason =
            CombatRefusalReason.None)
    {
        IsMapped = isMapped;
        HasCombatResult = hasCombatResult;
        CombatResult = combatResult;
        RefusalReason = refusalReason;
        DisplayName = displayName;
        CategoryZone = categoryZone;
    }

    public static RoutedGestureAction Unmapped(
        string label,
        int categoryZone)
    {
        return new RoutedGestureAction(
            false,
            false,
            CombatActionResult.Unavailable,
            label,
            categoryZone,
            CombatRefusalReason.None
        );
    }
}

public sealed class CombatGestureCommandRouter
{
    private const int MiddleDefenseZone = 4;
    private const int MiddleMovementZone = 7;
    private const int ContextMovementZone = 9;

    private readonly FighterCombat fighter;

    public bool IsDead => fighter == null || fighter.IsDead;
    public float PermutationFeedbackDuration =>
        fighter != null
            ? fighter.Rules.PermutationFeedbackDuration
            : 0.35f;
    public bool ShouldCancelInput =>
        fighter == null ||
        fighter.CurrentState is
            FighterCombatState.Stunned or
            FighterCombatState.Dead;

    public CombatGestureCommandRouter(FighterCombat controlledFighter)
    {
        fighter = controlledFighter;
    }

    public RoutedGestureAction ExecuteTap(int zone)
    {
        if (fighter == null)
            return RoutedGestureAction.Unmapped(
                "Commande indisponible",
                zone
            );

        if (zone is >= 0 and <= 2)
        {
            return Action(
                fighter.LightAttack(),
                $"Attaque {(char)('A' + zone)}",
                zone
            );
        }

        if (zone is >= 3 and <= 5)
        {
            return Action(
                fighter.StartDefense(),
                DefenseDisplayName(zone),
                zone
            );
        }

        return RoutedGestureAction.Unmapped(
            "Non assigné",
            zone
        );
    }

    public RoutedGestureAction BeginHold(int zone)
    {
        if (fighter == null)
            return RoutedGestureAction.Unmapped(
                "Commande indisponible",
                zone
            );

        if (zone == MiddleDefenseZone)
        {
            return Action(
                fighter.StartHeldGuard(),
                "Garde maintenue",
                zone
            );
        }

        if (zone == MiddleMovementZone)
        {
            return Action(
                fighter.StartCharge(),
                "Recharge",
                zone
            );
        }

        return RoutedGestureAction.Unmapped(
            "Non assigné",
            zone
        );
    }

    public void EndHold(int zone)
    {
        if (fighter == null)
            return;

        if (zone == MiddleDefenseZone)
            fighter.StopHeldGuard();
        else if (zone == MiddleMovementZone)
            fighter.StopChargeInput();
    }

    public RoutedGestureAction BeginStrokeHold(
        int destinationZone)
    {
        if (fighter == null)
        {
            return RoutedGestureAction.Unmapped(
                "Commande indisponible",
                destinationZone
            );
        }

        SpatialMovementType movementType =
            destinationZone switch
            {
                6 => SpatialMovementType.StrafeLeft,
                8 => SpatialMovementType.StrafeRight,
                _ => SpatialMovementType.None
            };

        if (movementType == SpatialMovementType.None)
        {
            return RoutedGestureAction.Unmapped(
                "Non assigné",
                destinationZone
            );
        }

        return Action(
            fighter.StartSpatialMovement(movementType),
            SpatialMovementDisplayName(movementType),
            destinationZone
        );
    }

    public void EndStrokeHold()
    {
        fighter?.StopSpatialMovement();
    }

    public RoutedGestureAction ExecuteDistanceDodge(
        int destinationZone)
    {
        if (fighter == null)
        {
            return RoutedGestureAction.Unmapped(
                "Commande indisponible",
                destinationZone
            );
        }

        return destinationZone switch
        {
            MiddleDefenseZone => Action(
                fighter.DodgeForward(),
                "Esquive avant",
                destinationZone
            ),
            ContextMovementZone => Action(
                fighter.DodgeBackward(),
                "Esquive arriere",
                destinationZone
            ),
            _ => RoutedGestureAction.Unmapped(
                "Non assigne",
                destinationZone
            )
        };
    }

    public RoutedGestureAction TryPermutation(
        long commandToken)
    {
        if (fighter == null)
        {
            return RoutedGestureAction.Unmapped(
                "Commande indisponible",
                6
            );
        }

        return Action(
            fighter.TryPermutation(commandToken),
            "Permutation",
            6
        );
    }

    public RoutedGestureAction ExecuteStroke(
        GestureRecognitionResult recognition,
        long commandToken = 0)
    {
        if (fighter == null || !recognition.IsRecognized)
        {
            return RoutedGestureAction.Unmapped(
                "Commande invalide",
                FirstZone(recognition)
            );
        }

        switch (recognition.GestureId)
        {
            case CombatGestureId.DodgeRight:
                return Action(
                    fighter.DodgeRight(),
                    "Esquive droite",
                    FirstZone(recognition)
                );

            case CombatGestureId.DodgeLeft:
                return Action(
                    fighter.DodgeLeft(),
                    "Esquive gauche",
                    FirstZone(recognition)
                );

            case CombatGestureId.GrandV:
                return RoutedGestureAction.Unmapped(
                    "Non assigné",
                    FirstZone(recognition)
                );

            case CombatGestureId.Permutation:
                return TryPermutation(commandToken);

            default:
                return RoutedGestureAction.Unmapped(
                    "Non assigné",
                    FirstZone(recognition)
                );
        }
    }

    private RoutedGestureAction Action(
        CombatActionResult result,
        string displayName,
        int categoryZone)
    {
        CombatRefusalReason refusalReason =
            result == CombatActionResult.Started ||
            fighter == null
                ? CombatRefusalReason.None
                : fighter.LastRefusalReason;

        return new RoutedGestureAction(
            true,
            true,
            result,
            displayName,
            categoryZone,
            refusalReason
        );
    }

    private static string SpatialMovementDisplayName(
        SpatialMovementType movementType)
    {
        return movementType switch
        {
            SpatialMovementType.Advance => "Avancer",
            SpatialMovementType.Retreat => "Reculer",
            SpatialMovementType.StrafeLeft =>
                "Marche gauche",
            SpatialMovementType.StrafeRight =>
                "Marche droite",
            _ => "Déplacement"
        };
    }

    private static string DefenseDisplayName(int zone)
    {
        return zone switch
        {
            3 => "Défense gauche",
            4 => "Défense centrale",
            5 => "Défense droite",
            _ => "Défense"
        };
    }

    private static int FirstZone(
        GestureRecognitionResult recognition)
    {
        return recognition.Zones != null &&
            recognition.Zones.Count > 0
                ? recognition.Zones[0]
                : -1;
    }
}
