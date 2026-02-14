using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Core.Models.Game;

public class GameManager : IGameManager
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IMechFactory _mechFactory;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IDamageTransferCalculator _damageTransferCalculator;
    private readonly ICriticalHitsCalculator _criticalHitsCalculator;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IFallProcessor _fallProcessor;
    private readonly IConsciousnessCalculator _consciousnessCalculator;
    private readonly IHeatEffectsCalculator _heatEffectsCalculator;
    private readonly IGameFactory _gameFactory;
    private ServerGame? _serverGame;
    private readonly INetworkHostService? _networkHostService;
    private bool _isDisposed;
    private readonly ILocalizationService _localizationService;
    private readonly ICommandLoggerFactory _commandLoggerFactory;
    private ICommandLogger? _commandLogger;
    private Action<IGameCommand>? _logHandler;

    public GameManager(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor,
        IGameFactory gameFactory,
        ILocalizationService localizationService,
        ICommandLoggerFactory commandLoggerFactory,
        INetworkHostService? networkHostService = null)
    {
        _rulesProvider = rulesProvider;
        _mechFactory = mechFactory;

        _commandPublisher = commandPublisher;
        _diceRoller = diceRoller;
        _toHitCalculator = toHitCalculator;
        _damageTransferCalculator = damageTransferCalculator;
        _criticalHitsCalculator = criticalHitsCalculator;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _fallProcessor = fallProcessor;
        _consciousnessCalculator = consciousnessCalculator;
        _heatEffectsCalculator = heatEffectsCalculator;
        _gameFactory = gameFactory;
        _localizationService = localizationService;
        _commandLoggerFactory = commandLoggerFactory;
        _networkHostService = networkHostService;
    }
    
    private static Action<IGameCommand> SafeLog(ICommandLogger logger) =>
        command =>
        {
            try
            {
                logger.Log(command);
            }
            catch
            {
                // Swallow to avoid impacting a publisher
            }
        };

    public async Task ResetForNewGame()
    {
        // Dispose current server game if exists
        if (_serverGame != null)
        {
            _serverGame.Dispose();

            // Wait a bit for disposal to complete
            await Task.Delay(200);

            _serverGame = null;
        }

        // Unsubscribe logging handler
        if (_logHandler != null)
        {
            _commandPublisher.Unsubscribe(_logHandler);
            _logHandler = null;
        }

        // Dispose command logger
        _commandLogger?.Dispose();
        _commandLogger = null;
    }

    public async Task InitializeLobby()
    {
        // Reset before initializing new lobby
        await ResetForNewGame();

        var transportAdapter = _commandPublisher.Adapter;
        // Start the network host if supported and not already running
        if (CanStartLanServer && !IsLanServerRunning && _networkHostService != null)
        {
            await _networkHostService.Start();

            // Add the network publisher to the transport adapter if successfully started
            if (_networkHostService.Publisher != null)
            {
                transportAdapter.AddPublisher(_networkHostService.Publisher);
            }
        }

        // Create the game server instance
        _serverGame = _gameFactory.CreateServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _damageTransferCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor
        );
        // Start server listening loop in background
        _ = Task.Run(() => _serverGame?.Start());

        // Setup logging
        var transportPublisher = transportAdapter.TransportPublishers.FirstOrDefault(ta => ta is RxTransportPublisher);
        _commandLogger = _commandLoggerFactory.CreateLogger(_localizationService, _serverGame);

        _logHandler = SafeLog(_commandLogger);
        _commandPublisher.Subscribe(_logHandler, transportPublisher);
    }

    public void SetBattleMap(BattleMap battleMap)
    {
        _serverGame?.SetBattleMap(battleMap);
    }

    public string? GetLanServerAddress()
    {
        // Return address only if the host service is actually running
        return !IsLanServerRunning ? null : _networkHostService?.HubUrl;
    }
    
    public bool IsLanServerRunning => _networkHostService?.IsRunning ?? false;
    public bool CanStartLanServer => _networkHostService?.CanStart ?? false;
    public Guid? ServerGameId => _serverGame?.Id;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;
        
        // Dispose network host
        _networkHostService?.Dispose();
        _commandLogger?.Dispose();
    }
}