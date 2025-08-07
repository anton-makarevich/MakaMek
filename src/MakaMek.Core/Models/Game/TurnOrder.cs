using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game;

public class TurnOrder
{
    private readonly List<TurnStep> _steps = new();
    private int _currentStepIndex = -1;

    public IReadOnlyList<TurnStep> Steps => _steps.AsReadOnly();
    public TurnStep? CurrentStep => _currentStepIndex >= 0 && _currentStepIndex < _steps.Count 
        ? _steps[_currentStepIndex] 
        : null;

    public bool HasNextStep => _currentStepIndex < _steps.Count - 1;

    public void CalculateOrder(IReadOnlyList<IPlayer> initiativeOrder)
    {
        _steps.Clear();
        _currentStepIndex = -1;

        // Get unit counts for each player
        var unitCounts = initiativeOrder
            .ToDictionary(p => p, p => p.AliveUnits.Count);

        // If only one player, move all units
        if (unitCounts.Count == 1)
        {
            _steps.Add(new TurnStep(unitCounts.First().Key, unitCounts.First().Value));
            return;
        }
        
        // Keep track of remaining units for each player
        var remainingUnits = new Dictionary<IPlayer, int>(unitCounts);

        while (remainingUnits.Any(p => p.Value > 0))
        {
            // Find player with least units
            var minUnits = remainingUnits.Values.Where(v => v > 0).Min();

            // Process players in reverse initiative order (loser moves first)
            foreach (var player in initiativeOrder.Reverse())
            {
                if (!remainingUnits.ContainsKey(player) || remainingUnits[player] <= 0)
                    continue;

                // Calculate how many units this player should move based on ratio
                // If a team has N times as many units as the minimum, they move N units
                var ratio = remainingUnits[player] / minUnits;
                var unitsToMove = Math.Max(1, ratio); // At least 1 unit
                unitsToMove = Math.Min(unitsToMove, remainingUnits[player]); // Don't move more units than remaining

                _steps.Add(new TurnStep(player, unitsToMove));
                remainingUnits[player] -= unitsToMove;
            }
        }
    }

    public TurnStep? GetNextStep()
    {
        _currentStepIndex++;
        return CurrentStep;
    }

    public void Reset()
    {
        _currentStepIndex = -1;
    }
}