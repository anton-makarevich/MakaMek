using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Factories;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Factory;

public class GameFactoryTests
{
    private readonly IRulesProvider _rulesProvider= Substitute.For<IRulesProvider>();
    private readonly ICommandPublisher _commandPublisher= Substitute.For<ICommandPublisher>();
    private readonly IDiceRoller _diceRoller= Substitute.For<IDiceRoller>();
    private readonly IToHitCalculator _toHitCalculator= Substitute.For<IToHitCalculator>();
    private readonly IDamageTransferCalculator _damageTransferCalculator= Substitute.For<IDamageTransferCalculator>();
    private readonly ICriticalHitsCalculator _criticalHitsCalculator= Substitute.For<ICriticalHitsCalculator>();
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator= Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator= Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator= Substitute.For<IHeatEffectsCalculator>();
    private readonly IBattleMapFactory _mapFactory= Substitute.For<IBattleMapFactory>();
    private readonly IMechFactory _mechFactory= Substitute.For<IMechFactory>();
    private readonly IHashService _hashService= Substitute.For<IHashService>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly GameFactory _sut;

    public GameFactoryTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _sut = new GameFactory(_loggerFactory);
    }

    [Fact]
    public void CreateServerGame_ReturnsServerGameInstance()
    {
        // Act
        var serverGame = _sut.CreateServerGame(
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
            Substitute.For<IFallProcessor>());

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
            _mechFactory,
            _commandPublisher, 
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService);

        // Assert
        clientGame.ShouldNotBeNull();
        clientGame.ShouldBeOfType<ClientGame>();
    }
}
