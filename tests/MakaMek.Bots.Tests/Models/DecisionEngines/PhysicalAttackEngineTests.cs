using NSubstitute;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class PhysicalAttackEngineTests
{
    private readonly IClientGame _clientGame = Substitute.For<IClientGame>();
    private readonly IPhysicalAttackEligibility _eligibility = Substitute.For<IPhysicalAttackEligibility>();
    private readonly IPlayer _player = Substitute.For<IPlayer>();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly PhysicalAttackEngine _sut;

    public PhysicalAttackEngineTests()
    {
        _clientGame.Id.Returns(Guid.NewGuid());
        _clientGame.TurnPhase.Returns(PhaseNames.PhysicalAttack);
        _clientGame.BattleMap.Returns(_map);
        _clientGame.PhysicalAttackEligibility.Returns(_eligibility);
        _clientGame.DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>()).Returns(true);

        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        _clientGame.Players.Returns([_player]);

        _sut = new PhysicalAttackEngine(_clientGame);
    }

    [Fact]
    public async Task MakeDecision_SendsANoAttackDeclarationForEveryPendingUnit()
    {
        // Arrange
        var first = CreateUnit(hasDeclared: false);
        var second = CreateUnit(hasDeclared: false);
        _player.AliveUnits.Returns([first, second]);
        _eligibility.HasAnyOption(Arg.Any<IUnit>(), Arg.Any<IEnumerable<IUnit>>(), _map).Returns(true);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.Received(1).DeclarePhysicalAttack(Arg.Is<PhysicalAttackCommand>(cmd =>
            cmd.PlayerId == _player.Id && cmd.UnitId == first.Id && cmd.Declaration == null));
        await _clientGame.Received(1).DeclarePhysicalAttack(Arg.Is<PhysicalAttackCommand>(cmd =>
            cmd.PlayerId == _player.Id && cmd.UnitId == second.Id && cmd.Declaration == null));
    }

    [Fact]
    public async Task MakeDecision_SkipsUnitsThatAlreadyDeclared()
    {
        // Arrange
        var unit = CreateUnit(hasDeclared: true);
        _player.AliveUnits.Returns([unit]);
        _eligibility.HasAnyOption(Arg.Any<IUnit>(), Arg.Any<IEnumerable<IUnit>>(), _map).Returns(true);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.DidNotReceive().DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public async Task MakeDecision_SkipsUnitsWithoutEligibleOptions()
    {
        // Arrange
        var unit = CreateUnit(hasDeclared: false);
        _player.AliveUnits.Returns([unit]);
        _eligibility.HasAnyOption(unit, Arg.Any<IEnumerable<IUnit>>(), _map).Returns(false);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.DidNotReceive().DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNotThePhysicalAttackPhase_DoesNothing()
    {
        // Arrange
        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        var unit = CreateUnit(hasDeclared: false);
        _player.AliveUnits.Returns([unit]);
        _eligibility.HasAnyOption(Arg.Any<IUnit>(), Arg.Any<IEnumerable<IUnit>>(), _map).Returns(true);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.DidNotReceive().DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_DoesNothing()
    {
        // Arrange
        _clientGame.BattleMap.Returns((IBattleMap?)null);
        var unit = CreateUnit(hasDeclared: false);
        _player.AliveUnits.Returns([unit]);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.DidNotReceive().DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public async Task MakeDecision_StopsAfterARejectedDeclaration()
    {
        // Arrange
        var first = CreateUnit(hasDeclared: false);
        var second = CreateUnit(hasDeclared: false);
        _player.AliveUnits.Returns([first, second]);
        _eligibility.HasAnyOption(Arg.Any<IUnit>(), Arg.Any<IEnumerable<IUnit>>(), _map).Returns(true);
        _clientGame.DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>()).Returns(false);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.Received(1).DeclarePhysicalAttack(Arg.Any<PhysicalAttackCommand>());
    }

    private static IUnit CreateUnit(bool hasDeclared)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.HasDeclaredPhysicalAttack.Returns(hasDeclared);
        return unit;
    }
}
