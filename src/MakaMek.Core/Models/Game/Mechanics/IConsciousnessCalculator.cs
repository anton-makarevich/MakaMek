using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating pilot consciousness rolls
/// </summary>
public interface IConsciousnessCalculator
{
    /// <summary>
    /// Makes consciousness rolls for all pending consciousness numbers in the pilot's queue
    /// </summary>
    /// <param name="pilot">The pilot to make consciousness rolls for</param>
    /// <returns>List of consciousness roll commands, empty if pilot is already unconscious or dead</returns>
    IEnumerable<PilotConsciousnessRollCommand> MakeConsciousnessRolls(IPilot pilot);
    
    /// <summary>
    /// Makes a recovery consciousness roll for an unconscious pilot
    /// </summary>
    /// <param name="pilot">The pilot to make a recovery roll for</param>
    /// <returns>Recovery roll command, null if pilot is conscious or dead</returns>
    PilotConsciousnessRollCommand? MakeRecoveryConsciousnessRoll(IPilot pilot);
}
