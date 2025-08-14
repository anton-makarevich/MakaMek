using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.Transport;
using Shouldly;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class GameManagerTests : IDisposable
{
    private readonly GameManager _sut;
    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IWeaponSelectionCalculator _weaponSelectionCalculator = Substitute.For<IWeaponSelectionCalculator>();
    private readonly CommandTransportAdapter _transportAdapter;
    private readonly IGameFactory _gameFactory;
    private readonly ServerGame _serverGame;
    private readonly INetworkHostService _networkHostService;
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();
    private readonly ICriticalHitsCalculator _criticalHitsCalculator = Substitute.For<ICriticalHitsCalculator>();
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly IFallProcessor _fallProcessor = Substitute.For<IFallProcessor>();

    public GameManagerTests()
    {
        _rulesProvider = Substitute.For<IRulesProvider>();
        _commandPublisher = Substitute.For<ICommandPublisher>();
        _diceRoller = Substitute.For<IDiceRoller>();
        _toHitCalculator = Substitute.For<IToHitCalculator>();
        // Use a real adapter with a mock publisher for testing AddPublisher calls
        var initialPublisher = Substitute.For<ITransportPublisher>(); 
        _transportAdapter = new CommandTransportAdapter([initialPublisher]);
        _gameFactory = Substitute.For<IGameFactory>();
        _networkHostService = Substitute.For<INetworkHostService>();

        _serverGame = new ServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor
            );
        _gameFactory.CreateServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor).Returns(_serverGame);
        _commandPublisher.Adapter.Returns(_transportAdapter);

        _sut = new GameManager(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor,
            _gameFactory,
            _networkHostService);
    }

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndNotRunning_StartsNetworkHostAndAddsPublisher()
    {
        // Arrange
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.Received(1).Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(2); // Initial mock + network publisher
        _transportAdapter.TransportPublishers.ShouldContain(networkPublisher);
        _gameFactory.Received(1).CreateServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor);
    }
    
    [Fact]
    public async Task InitializeLobby_CreatesServerGame()
    {
        // Act
        await _sut.InitializeLobby();

        // Assert
        _sut.ServerGameId.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndNetworkPublisherIsNull_StartsNetworkHostButDoesNotAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.CanStart.Returns(true);
        _networkHostService.Publisher.Returns((ITransportPublisher?)null);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.Received(1).Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1); // Only the initial mock publisher
        _gameFactory.Received(1).CreateServerGame(
            _rulesProvider, 
            _mechFactory,
            _commandPublisher, 
            _diceRoller, 
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor);
    }

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndAlreadyRunning_DoesNotStartNetworkHost()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(true);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.DidNotReceive().Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1); // Only initial mock publisher
        _gameFactory.Received(1).CreateServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor);
    }

    [Fact]
    public async Task InitializeLobby_WhenNetworkHostNotSupported_DoesNotStartNetworkHostOrAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.CanStart.Returns(false);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.DidNotReceive().Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1);
        _gameFactory.Received(1).CreateServerGame(
            _rulesProvider, 
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor);
    }

    [Fact]
    public void GetLanServerAddress_WhenRunning_ReturnsHubUrl()
    {
        // Arrange
        var expectedUrl = "http://localhost:2439";
        _networkHostService.IsRunning.Returns(true);
        _networkHostService.HubUrl.Returns(expectedUrl);

        // Act
        var result = _sut.GetLanServerAddress();

        // Assert
        result.ShouldBe(expectedUrl);
    }

    [Fact]
    public void GetLanServerAddress_WhenNotRunning_ReturnsNull()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);

        // Act
        var result = _sut.GetLanServerAddress();

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsLanServerRunning_ReturnsCorrectValueFromNetworkHost(bool isRunning)
    {
        // Arrange
        _networkHostService.IsRunning.Returns(isRunning);

        // Act & Assert
        _sut.IsLanServerRunning.ShouldBe(isRunning);
    }

    [Fact]
    public void IsLanServerRunning_WhenHostIsNull_ReturnsFalse()
    {
        // Arrange
        var sutWithNullHost = new GameManager(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor,
            _gameFactory);

        // Act & Assert
        sutWithNullHost.IsLanServerRunning.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanStartLanServer_ReturnsCorrectValueFromNetworkHost(bool canStart)
    {
        // Arrange
        _networkHostService.CanStart.Returns(canStart);

        // Act & Assert
        _sut.CanStartLanServer.ShouldBe(canStart);
    }

    [Fact]
    public void CanStartLanServer_WhenHostIsNull_ReturnsFalse()
    {
        // Arrange
        var sutWithNullHost = new GameManager(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor,
            _gameFactory);

        // Act & Assert
        sutWithNullHost.CanStartLanServer.ShouldBeFalse();
    }

    [Fact]
    public async Task StartServer_CalledMultipleTimes_StartsServerAndNetworkHostOnlyOnce()
    {
        // Arrange
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false); // Start as not running
        _networkHostService.Publisher.Returns(networkPublisher);

        // Act
        await _sut.InitializeLobby(); // First call, enable LAN
        _networkHostService.IsRunning.Returns(true);  // Simulate network host is now running
        await _sut.InitializeLobby(); // Second call

        // Assert
        await _networkHostService.Received(1).Start(); // Should only be called once
        _transportAdapter.TransportPublishers.Count.ShouldBe(2); // Publisher should only be added once
        _transportAdapter.TransportPublishers.ShouldContain(networkPublisher);
        _gameFactory.Received(1).CreateServerGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor);
    }

    [Fact]
    public void Dispose_CallsNetworkHostDispose()
    {
        // Act
        _sut.Dispose();

        // Assert
        _networkHostService.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_WhenHostIsNull_DoesNotThrow()
    {
        // Arrange
        var sutWithNullHost = new GameManager(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _weaponSelectionCalculator,
            _criticalHitsCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor,
            _gameFactory);

        // Act & Assert
        Should.NotThrow(() => sutWithNullHost.Dispose());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DisposesHostOnlyOnce()
    {
        // Act
        _sut.Dispose();
        _sut.Dispose(); // Call again

        // Assert
        _networkHostService.Received(1).Dispose(); // Should still be called only once
    }

    [Fact]
    public async Task SetBattleMap_CallsSetBattleMapOnServerGame()
    {
        // Arrange
        await _sut.InitializeLobby(); // Ensure _serverGame is created via factory
        var battleMap = new BattleMap(10, 10);

        // Act
        _serverGame.BattleMap.ShouldBeNull();
        _sut.SetBattleMap(battleMap);

        // Assert
        _serverGame.BattleMap.ShouldBe(battleMap); // Verify the map was set
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
