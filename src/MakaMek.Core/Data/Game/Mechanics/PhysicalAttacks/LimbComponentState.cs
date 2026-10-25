using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// Neutral state of one component mounted at an attacking limb, used by the rules layer to
/// select the actuators that reduce physical attack damage without referencing component classes.
/// </summary>
/// <param name="Type">The component kind.</param>
/// <param name="IsDestroyed">Whether the component is destroyed.</param>
public record LimbComponentState(MakaMekComponent Type, bool IsDestroyed);
