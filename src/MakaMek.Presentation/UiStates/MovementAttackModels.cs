using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// One legal target with the cheapest continuation path from the current path state that reaches its
/// hex and passes the shared declaration validator, plus the server-computed declaration for that
/// path.
/// </summary>
public sealed record MovementAttackCandidate(
    IUnit Target,
    MovementPath Path,
    PhysicalAttackDeclarationData Declaration);
