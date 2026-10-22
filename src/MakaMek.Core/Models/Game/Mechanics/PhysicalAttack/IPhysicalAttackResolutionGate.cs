using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// A check the physical attack resolution phase runs on each declared limb before resolving it.
/// The weapon <c>IAttackResolutionGate</c> takes a weapon and its slot, so physical attacks have
/// their own gate over a <see cref="PhysicalAttackContext"/>.
/// </summary>
public interface IPhysicalAttackResolutionGate
{
    /// <summary>
    /// True when the attack has to be skipped. A gate that skips logs why.
    /// </summary>
    bool ShouldSkip(PhysicalAttackContext context, ILogger logger);
}
