namespace Sanet.MakaMek.Bots.Data;

public readonly record struct MovementPhaseState(
    int EnemyUnitsRemaining,
    int FriendlyUnitsRemaining,
    PhaseState Phase
);