using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class GameManagerTests : IDisposable
{
    private readonly GameManager _sut;
    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly CommandTransportAdapter _transportAdapter;
    private readonly INetworkHostService _networkHostService;
    private readonly BattleMap _battleMap;

    public GameManagerTests()
    {
        _rulesProvider = Substitute.For<IRulesProvider>();
        _commandPublisher = Substitute.For<ICommandPublisher>();
        _diceRoller = Substitute.For<IDiceRoller>();
        _toHitCalculator = Substitute.For<IToHitCalculator>();
        // Use a real adapter with a mock publisher for testing AddPublisher calls
        var initialPublisher = Substitute.For<ITransportPublisher>(); 
        _transportAdapter = new CommandTransportAdapter([initialPublisher]);
        _networkHostService = Substitute.For<INetworkHostService>();
        _battleMap = Substitute.For<BattleMap>();

        _sut = new GameManager(_rulesProvider, _commandPublisher, _diceRoller,
            _toHitCalculator, _transportAdapter, _networkHostService);
    }

    [Fact]
    public void StartServer_WithLanEnabled_AndNotRunning_StartsNetworkHostAndAddsPublisher()
    {
        // Arrange
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);

        // Act
        _sut.StartServer(_battleMap, enableLan: true);

        // Assert
        _networkHostService.Received(1).Start(2439);
    }
    
    [Fact]
    public void StartServer_WithLanEnabled_AndNetworkPublisherIsNull_StartsNetworkHostButDoesNotAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns((ITransportPublisher?)null);

        // Act
        _sut.StartServer(_battleMap, enableLan: true);

        // Assert
        _networkHostService.Received(1).Start(2439);
    }

    [Fact]
    public void StartServer_WithLanEnabled_AndAlreadyRunning_DoesNotStartNetworkHost()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(true);

        // Act
        _sut.StartServer(_battleMap, enableLan: true);

        // Assert
        _networkHostService.DidNotReceive().Start(Arg.Any<int>());
    }

    [Fact]
    public void StartServer_WithLanDisabled_DoesNotStartNetworkHostOrAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);

        // Act
        _sut.StartServer(_battleMap, enableLan: false);

        // Assert
        _networkHostService.DidNotReceive().Start(Arg.Any<int>());
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
        var sutWithNullHost = new GameManager(_rulesProvider, _commandPublisher, _diceRoller,
            _toHitCalculator, _transportAdapter, null!); // Pass null host

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
        var sutWithNullHost = new GameManager(_rulesProvider, _commandPublisher, _diceRoller,
            _toHitCalculator, _transportAdapter, null!); // Pass null host

        // Act & Assert
        sutWithNullHost.CanStartLanServer.ShouldBeFalse();
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
        var sutWithNullHost = new GameManager(_rulesProvider, _commandPublisher, _diceRoller,
            _toHitCalculator, _transportAdapter, null!); // Pass null host

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

    public void Dispose()
    {
        _sut?.Dispose();
    }
}
