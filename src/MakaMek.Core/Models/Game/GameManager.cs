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
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Core.Models.Game;

public class GameManager : IGameManager
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IMechFactory _mechFactory;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IStructureDamageCalculator _structureDamageCalculator;
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
    private bool _loggingSubscribed;

    public GameManager(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IStructureDamageCalculator structureDamageCalculator,
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
        _structureDamageCalculator = structureDamageCalculator;
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

    public async Task InitializeLobby()
    {
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
        
        // Create the game server instance if not already created
        if (_serverGame == null)
        {
            _serverGame = _gameFactory.CreateServerGame(
                _rulesProvider,
                _mechFactory,
                _commandPublisher,
                _diceRoller,
                _toHitCalculator,
                _structureDamageCalculator,
                _criticalHitsCalculator,
                _pilotingSkillCalculator,
                _consciousnessCalculator,
                _heatEffectsCalculator,
                _fallProcessor
            );
            // Start server listening loop in background
            _ = Task.Run(() => _serverGame?.Start());
        }
        if (!_loggingSubscribed)
        {
            var transportPublisher = transportAdapter.TransportPublishers.FirstOrDefault(ta => ta is RxTransportPublisher);
            _commandLogger = _commandLoggerFactory.CreateFileLogger(_localizationService, _serverGame);
            
            _logHandler = SafeLog(_commandLogger);
            _commandPublisher.Subscribe(_logHandler, transportPublisher);
            _loggingSubscribed = true;
        }
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