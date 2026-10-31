using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One limb combination a physical attack can be declared with, together with its localized
/// label, for example "Punch (Both Arms)" or "Kick (LL)".
/// </summary>
public sealed record PhysicalAttackLimbVariant(string Label, IReadOnlyList<PartLocation> Limbs);
