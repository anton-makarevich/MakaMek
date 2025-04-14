using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Factory;

public class GameFactoryTests
{
    private readonly GameFactory _sut= new GameFactory();
    private readonly IRulesProvider _rulesProvider= Substitute.For<IRulesProvider>();
    private readonly ICommandPublisher _commandPublisher= Substitute.For<ICommandPublisher>();
    private readonly IDiceRoller _diceRoller= Substitute.For<IDiceRoller>();
    private readonly IToHitCalculator _toHitCalculator= Substitute.For<IToHitCalculator>();
    private readonly IBattleMapFactory _mapFactory= Substitute.For<IBattleMapFactory>();

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
            _toHitCalculator,
            _mapFactory);

        // Assert
        clientGame.ShouldNotBeNull();
        clientGame.ShouldBeOfType<ClientGame>();
    }
}
