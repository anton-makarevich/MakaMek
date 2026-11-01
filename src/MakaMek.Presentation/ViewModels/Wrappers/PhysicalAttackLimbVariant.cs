using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One limb combination a physical attack can be declared with, together with its localized
/// label, for example "Punch (Both Arms)" or "Kick (LL)", the damage it would deal and, when the
/// combination cannot be declared, the reason why.
/// </summary>
public sealed record PhysicalAttackLimbVariant(
    string Label,
    IReadOnlyList<PartLocation> Limbs,
    int Damage = 0,
    PhysicalAttackRestriction? Restriction = null)
{
    public bool IsAvailable => Restriction is null;

    public bool HasDamage => Damage > 0;
}
