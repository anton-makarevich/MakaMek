using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots;

/// <summary>
/// Manages the lifecycle of bot players in the game
/// </summary>
public class BotManager : IBotManager
{
    private readonly List<IBot> _bots = [];
    private ClientGame? _clientGame;

    public IReadOnlyList<IBot> Bots => _bots;

    public void Initialize(ClientGame clientGame)
    {
        // Clean up existing bots if reinitializing
        Clear();
        
        _clientGame = clientGame;
    }

    public void AddBot(IPlayer player, BotDifficulty difficulty = BotDifficulty.Easy)
    {
        if (_clientGame == null)
        {
            throw new InvalidOperationException("BotManager must be initialized with a ClientGame before adding bots");
        }

        // TODO: Join the game with the bot's units
        // This will be implemented in Task 0.2 when ClientGame is modified
        // _clientGame.JoinGameWithUnits(player, units, pilotAssignments, isBot: true);

        // Create the bot player
        var bot = new Bot(player, _clientGame, difficulty);
        _bots.Add(bot);
    }

    public void RemoveBot(Guid playerId)
    {
        var bot = _bots.FirstOrDefault(b => b.Player.Id == playerId);
        if (bot == null) return;

        bot.Dispose();
        _bots.Remove(bot);
        //TODO: should bot controlled players leave the game?
    }

    public void Clear()
    {
        foreach (var bot in _bots)
        {
            bot.Dispose();
        }
        _bots.Clear();
    }
}

