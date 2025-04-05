using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game;

public class GameManager : IGameManager
{
    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly CommandTransportAdapter _transportAdapter;
    private readonly IGameFactory _gameFactory;
    private ServerGame? _serverGame;
    private readonly INetworkHostService? _networkHostService;
    private bool _isDisposed;

    public GameManager(IRulesProvider rulesProvider, ICommandPublisher commandPublisher, IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator, CommandTransportAdapter transportAdapter, 
        IGameFactory gameFactory, 
        INetworkHostService? networkHostService = null)
    {
        _rulesProvider = rulesProvider;
        _commandPublisher = commandPublisher;
        _diceRoller = diceRoller;
        _toHitCalculator = toHitCalculator;
        _transportAdapter = transportAdapter;
        _gameFactory = gameFactory;
        _networkHostService = networkHostService;
    }

    public async Task InitializeLobby()
    {
        // Start the network host if supported and not already running
        if (CanStartLanServer && !IsLanServerRunning && _networkHostService != null)
        {
            await _networkHostService.Start(2439);
            
            // Add the network publisher to the transport adapter if successfully started
            if (_networkHostService.Publisher != null)
            {
                _transportAdapter.AddPublisher(_networkHostService.Publisher);
            }
        }
        
        // Create the game server instance if not already created
        if (_serverGame == null)
        {
            _serverGame = _gameFactory.CreateServerGame(
                _rulesProvider, _commandPublisher, _diceRoller, _toHitCalculator);
            // Start server listening loop in background
            _ = Task.Run(() => _serverGame?.Start());
        }
    }

    public void SetBattleMap(BattleMap battleMap)
    {
        _serverGame?.SetBattleMap(battleMap);
    }

    public string? GetLanServerAddress()
    {
        // Return address only if the host service is actually running
        if (!IsLanServerRunning)
        {
            return null;
        }
        
        return _networkHostService?.HubUrl;
    }
    
    public bool IsLanServerRunning => _networkHostService?.IsRunning ?? false;
    public bool CanStartLanServer => _networkHostService?.CanStart ?? false;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;
        
        // Dispose network host
        _networkHostService?.Dispose();
    }
}