using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Manages the lifecycle of bot players in the game
/// </summary>
public class BotManager : IBotManager
{
    private readonly Dictionary<Guid, IBot> _bots = new(); // Key: PlayerId

    public IClientGame? ClientGame { get; private set; }

    public IReadOnlyList<IBot> Bots => _bots.Values.ToList();

    public void Initialize(IClientGame clientGame)
    {
        // Clean up existing bots if reinitializing
        Clear();

        ClientGame = clientGame;
    }

    public void AddBot(IPlayer player, BotDifficulty difficulty = BotDifficulty.Easy)
    {
        if (ClientGame == null)
        {
            throw new InvalidOperationException("BotManager must be initialized with a ClientGame before adding bots");
        }

        // Ensure player has correct control type
        if (player.ControlType != PlayerControlType.Bot)
        {
            throw new ArgumentException("Player must have ControlType.Bot", nameof(player));
        }

        // Join the game with the bot's units
        ClientGame.JoinGameWithUnits(player,
            player.Units.Select(u => u.ToData())
                .ToList(),
            player.Units.Select(u => new PilotAssignmentData
        {
            UnitId = u.Id,
            PilotData = u.Pilot!.ToData()
        }).ToList());

        // Create decision engines for the bot
        var decisionEngines = new Dictionary<PhaseNames, IBotDecisionEngine>
        {
            { PhaseNames.Deployment, new DeploymentEngine(ClientGame, player, difficulty) },
            { PhaseNames.Movement, new MovementEngine(ClientGame, player, difficulty) },
            { PhaseNames.WeaponsAttack, new WeaponsEngine(ClientGame, player, difficulty) },
            { PhaseNames.End, new EndPhaseEngine(ClientGame, player, difficulty) }
        };
        var decisionEngineProvider = new DecisionEngineProvider(decisionEngines);

        // BotManager tracks which players are bots
        var bot = new Bot(player, ClientGame, difficulty, decisionEngineProvider);
        _bots.Add(player.Id, bot);
    }

    public void RemoveBot(Guid playerId)
    {
        if (_bots.TryGetValue(playerId, out var bot))
        {
            bot.Dispose();
            _bots.Remove(playerId);

            // Optionally remove player from game
            // _clientGame.RemovePlayer(playerId);
        }
    }

    public bool IsBot(Guid playerId)
    {
        return _bots.ContainsKey(playerId);
    }

    public void Clear()
    {
        foreach (var bot in _bots.Values)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
}

