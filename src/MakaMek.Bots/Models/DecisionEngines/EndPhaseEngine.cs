using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the end phase
/// </summary>
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    public EndPhaseEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null)
    {
        try
        {
            // 1. Handle shutdown units (attempt restart if probability is favorable)
            await HandleShutdownUnits(player);

            // 2. Handle overheated units (shutdown if heat > threshold or ammo risk)
            await HandleOverheatedUnits(player);

            // 3. End turn
            await EndTurn(player);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"EndPhaseEngine error for player {player.Name}: {ex.Message}");

            // Always try to end turn even if other actions failed
            await EndTurn(player);
        }
    }

    private async Task HandleShutdownUnits(IPlayer player)
    {
        var shutdownUnits = player.AliveUnits
            .Where(u => u.IsShutdown && u is Mech)
            .Cast<Mech>()
            .Where(ShouldAttemptStartup)
            .ToList();

        foreach (var mech in shutdownUnits)
        {
            var startupCommand = new StartupUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = mech.Id
            };

            await _clientGame.StartupUnit(startupCommand);
        }
    }

    private bool ShouldAttemptStartup(Mech mech)
    {
        // Check if startup is even possible
        if (!mech.CurrentShutdownData.HasValue)
            return false;

        var shutdownData = mech.CurrentShutdownData.Value;

        // Can't startup in the same turn as shutdown
        if (shutdownData.Turn >= _clientGame.Turn)
            return false;

        // Check if pilot is unconscious
        if (mech.Pilot?.IsConscious != true)
            return false;

        var currentHeat = mech.CurrentHeat;
        var avoidNumber = _clientGame.HeatEffectsCalculator.GetShutdownAvoidNumber(currentHeat);

        // Don't attempt if impossible
        if (avoidNumber >= DiceUtils.Impossible2D6Roll)
            return false;

        // Always attempt if automatic (very low heat)
        if (avoidNumber < DiceUtils.Guaranteed2D6Roll)
            return true;

        // Calculate success probability
        var probability = DiceUtils.Calculate2d6Probability(avoidNumber);

        // Only attempt if probability is reasonable (>= 50%)
        // This threshold can be adjusted based on bot difficulty/aggression
        const double minimumProbabilityThreshold = 50.0;
        
        return probability >= minimumProbabilityThreshold;
    }

    private async Task HandleOverheatedUnits(IPlayer player)
    {
        var unitsToShutdown = player.AliveUnits
            .Where(u => u is Mech && !u.IsShutdown)
            .Cast<Mech>()
            .Where(ShouldShutdown)
            .ToList();

        foreach (var mech in unitsToShutdown)
        {
            var shutdownCommand = new ShutdownUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = mech.Id
            };

            await _clientGame.ShutdownUnit(shutdownCommand);
        }
    }

    private bool ShouldShutdown(Mech mech)
    {
        var currentHeat = mech.CurrentHeat;

        // Shutdown at very high heat (approaching automatic shutdown at 30)
        if (currentHeat >= 26)
            return true;

        // Strategic shutdown to avoid ammo explosion if unit has ammo
        if (mech.HasAmmo)
        {
            // Ammo explosion thresholds are at heat 19, 23, 28
            // Shutdown before reaching these thresholds
            if (currentHeat >= 23)
                return true;
        }

        // Shutdown at moderately high heat if no immediate tactical need
        if (currentHeat >= 25)
        {
            // Could add tactical considerations here:
            // - Distance to nearest enemy
            // - Whether unit has useful weapons that can fire
            // - Whether unit is in danger
            // For now, use simple heat-based decision
            return true;
        }

        return false;
    }

    private async Task EndTurn(IPlayer player)
    {
        var endTurnCommand = new TurnEndedCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id
        };

        await _clientGame.EndTurn(endTurnCommand);
    }
}

