using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for pilot consciousness rolls following BattleTech rules
/// </summary>
public class ConsciousnessCalculator : IConsciousnessCalculator
{
    private readonly IDiceRoller _diceRoller;

    public ConsciousnessCalculator(IDiceRoller diceRoller)
    {
        _diceRoller = diceRoller;
    }

    public IEnumerable<PilotConsciousnessRollCommand> MakeConsciousnessRolls(IPilot pilot)
    {
        // If pilot is already unconscious, dead, or has no pending rolls, return empty
        if (!pilot.IsConscious || pilot.IsDead || pilot.PendingConsciousnessNumbers.Count == 0)
        {
            yield break;
        }

        // Process all pending consciousness numbers
        while (pilot.PendingConsciousnessNumbers.Count != 0)
        {
            var consciousnessNumber = pilot.PendingConsciousnessNumbers.Dequeue();
            var diceResults = _diceRoller.Roll2D6();
            var rollTotal = diceResults.Sum(d => d.Result);
            var isSuccessful = rollTotal >= consciousnessNumber;

            var command = new PilotConsciousnessRollCommand
            {
                UnitId = pilot.AssignedTo?.Id ?? Guid.Empty,
                PilotId = pilot.Id,
                ConsciousnessNumber = consciousnessNumber,
                DiceResults = diceResults.Select(d => d.Result).ToList(),
                IsSuccessful = isSuccessful,
                IsRecoveryAttempt = false,
                GameOriginId = Guid.Empty, // Will be set by the calling phase
                Timestamp = DateTime.UtcNow
            };

            yield return command;

            // If the roll failed, pilot becomes unconscious and we stop processing
            if (!isSuccessful)
            {
                // Clear any remaining pending rolls since pilot is now unconscious
                pilot.PendingConsciousnessNumbers.Clear();
                break;
            }
        }
    }

    public PilotConsciousnessRollCommand? MakeRecoveryConsciousnessRoll(IPilot pilot)
    {
        // Only unconscious, living pilots can attempt recovery
        if (pilot.IsConscious || pilot.IsDead)
        {
            return null;
        }

        var consciousnessNumber = pilot.CurrentConsciousnessNumber;
        var diceResults = _diceRoller.Roll2D6();
        var rollTotal = diceResults.Sum(d => d.Result);
        var isSuccessful = rollTotal >= consciousnessNumber;

        return new PilotConsciousnessRollCommand
        {
            UnitId = pilot.AssignedTo?.Id ?? Guid.Empty,
            PilotId = pilot.Id,
            ConsciousnessNumber = consciousnessNumber,
            DiceResults = diceResults.Select(d => d.Result).ToList(),
            IsSuccessful = isSuccessful,
            IsRecoveryAttempt = true,
            GameOriginId = Guid.Empty, // Will be set by the calling phase
            Timestamp = DateTime.UtcNow
        };
    }
}
