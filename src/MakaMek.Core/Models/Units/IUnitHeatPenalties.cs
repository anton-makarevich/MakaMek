using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

namespace Sanet.MakaMek.Core.Models.Units;

/// <summary>
/// The heat-related penalties a unit exposes to movement and attack calculations.
/// Segregated from <see cref="IUnit"/> so the core unit contract does not have to
/// reference every penalty type.
/// </summary>
public interface IUnitHeatPenalties
{
    HeatMovementPenalty? MovementHeatPenalty { get; }
    HeatRollModifier? AttackHeatPenalty { get; }
    EngineHeatPenalty? EngineHeatPenalty { get; }
    IReadOnlyList<RollModifier> MovementModifiers { get; }
}
