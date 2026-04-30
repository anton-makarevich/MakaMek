using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for RollModifier and its derived types
/// Enables proper serialization/deserialization of abstract RollModifier class
/// </summary>
public partial class RollModifierTypeResolver : PolymorphicTypeResolver<RollModifier>
{
}