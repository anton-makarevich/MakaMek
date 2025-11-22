using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots;

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

        // TODO: Join the game with the bot's units
        // This will be implemented when ClientGame is modified to support bot joining
        // _clientGame.JoinGameWithUnits(player, units, pilotAssignments);

        // BotManager tracks which players are bots
        var bot = new Bot(player, ClientGame, difficulty);
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

