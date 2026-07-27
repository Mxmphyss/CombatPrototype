public readonly struct RoutedGestureAction
{
    public bool IsMapped { get; }
    public bool HasCombatResult { get; }
    public CombatActionResult CombatResult { get; }
    public string Label { get; }
    public int CategoryZone { get; }

    public RoutedGestureAction(
        bool isMapped,
        bool hasCombatResult,
        CombatActionResult combatResult,
        string label,
        int categoryZone)
    {
        IsMapped = isMapped;
        HasCombatResult = hasCombatResult;
        CombatResult = combatResult;
        Label = label;
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
            categoryZone
        );
    }
}

public sealed class CombatGestureCommandRouter
{
    private const int MiddleDefenseZone = 4;
    private const int MiddleMovementZone = 7;

    private readonly FighterCombat fighter;

    public bool IsDead => fighter == null || fighter.IsDead;
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
                "Attaque legere",
                zone
            );
        }

        if (zone is >= 3 and <= 5)
        {
            return Action(
                fighter.StartDefense(),
                "Defense simple",
                zone
            );
        }

        return RoutedGestureAction.Unmapped(
            "Commande non assignee",
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
                "Recharge endurance",
                zone
            );
        }

        return RoutedGestureAction.Unmapped(
            "Maintien non assigne",
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

    public RoutedGestureAction ExecuteStroke(
        GestureRecognitionResult recognition)
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
                    "Grand V reconnu",
                    FirstZone(recognition)
                );

            default:
                return RoutedGestureAction.Unmapped(
                    "Commande non assignee",
                    FirstZone(recognition)
                );
        }
    }

    private static RoutedGestureAction Action(
        CombatActionResult result,
        string label,
        int categoryZone)
    {
        return new RoutedGestureAction(
            true,
            true,
            result,
            label,
            categoryZone
        );
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
