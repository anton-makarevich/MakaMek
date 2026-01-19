using MakaMek.Tools.BotContainer.Configuration;
using MakaMek.Tools.BotContainer.Services;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace MakaMek.Tools.BotContainer.Models;

/// <summary>
/// Bot manager that uses LLM-enabled decision engines.
/// </summary>
public class LlmBotManager : IBotManager
{
    private readonly Dictionary<Guid, IBot> _bots = new();
    private readonly BotAgentClient _botAgentClient;
    private readonly IOptions<BotAgentConfiguration> _botAgentConfig;
    private readonly ILoggerFactory _loggerFactory;

    private IClientGame? ClientGame { get; set; }
    public IDecisionEngineProvider? DecisionEngineProvider { get; private set; }
    public IReadOnlyList<IBot> Bots => _bots.Values.ToList();

    public LlmBotManager(
        BotAgentClient botAgentClient,
        IOptions<BotAgentConfiguration> botAgentConfig,
        ILoggerFactory loggerFactory)
    {
        _botAgentClient = botAgentClient;
        _botAgentConfig = botAgentConfig;
        _loggerFactory = loggerFactory;
    }

    public void Initialize(IClientGame clientGame)
    {
        // Clean up existing bots if reinitializing
        Clear();

        ClientGame = clientGame;

        // Create LLM-enabled decision engine provider
        DecisionEngineProvider = new LlmDecisionEngineProvider(
            clientGame,
            _botAgentClient,
            _botAgentConfig,
            _loggerFactory);
    }

    public void AddBot(IPlayer player)
    {
        if (ClientGame == null || DecisionEngineProvider == null)
        {
            throw new InvalidOperationException("LlmBotManager must be initialized before adding bots");
        }

        // Ensure player has correct control type
        if (player.ControlType != PlayerControlType.Bot)
        {
            throw new ArgumentException("Player must have ControlType.Bot", nameof(player));
        }

        // Create bot with LLM-enabled decision engine provider
        var bot = new Bot(player.Id, ClientGame, DecisionEngineProvider);
        _bots.Add(player.Id, bot);
    }

    public void RemoveBot(Guid playerId)
    {
        if (_bots.TryGetValue(playerId, out var bot))
        {
            bot.Dispose();
            _bots.Remove(playerId);
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

