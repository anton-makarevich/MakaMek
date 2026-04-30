using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for PilotingSkillRollContext and its derived types
/// Enables proper serialization/deserialization of PilotingSkillRollContext records
/// </summary>
public partial class PilotingSkillRollContextTypeResolver : PolymorphicTypeResolver<PilotingSkillRollContext>
{
}
