using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Factory;

public class GameFactoryTests
{
    private readonly GameFactory _sut;
    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;

    public GameFactoryTests()
    {
        _sut = new GameFactory();
        _rulesProvider = Substitute.For<IRulesProvider>();
        _commandPublisher = Substitute.For<ICommandPublisher>();
        _diceRoller = Substitute.For<IDiceRoller>();
        _toHitCalculator = Substitute.For<IToHitCalculator>();
    }

    [Fact]
    public void CreateServerGame_ReturnsServerGameInstance()
    {
        // Act
        var serverGame = _sut.CreateServerGame(
            _rulesProvider, 
            _commandPublisher, 
            _diceRoller, 
            _toHitCalculator);

        // Assert
        serverGame.ShouldNotBeNull();
        serverGame.ShouldBeOfType<ServerGame>();
        // Implicitly checks if dependencies were passed, as constructor would fail otherwise
    }

    [Fact]
    public void CreateClientGame_ReturnsClientGameInstance()
    {
        // Act
        var clientGame = _sut.CreateClientGame(
            _rulesProvider, 
            _commandPublisher, 
            _toHitCalculator);

        // Assert
        clientGame.ShouldNotBeNull();
        clientGame.ShouldBeOfType<ClientGame>();
        // Implicitly checks if dependencies were passed
    }
}
