using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class PhysicalAttackCommandTests
{
    private readonly ILocalizationService _localizationService = TestLocalizationService.Create();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Player _player1 = new(Guid.NewGuid(), "Player 1", PlayerControlType.Human);
    private readonly Player _player2 = new(Guid.NewGuid(), "Player 2", PlayerControlType.Human);
    private readonly Unit _attacker;
    private readonly Unit _target;

    public PhysicalAttackCommandTests()
    {
        _game.Players.Returns([_player1, _player2]);
        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);

        var attackerData = MechFactoryTests.CreateDummyMechData();
        attackerData.Id = Guid.NewGuid();
        _attacker = mechFactory.Create(attackerData);
        _player1.AddUnit(_attacker);

        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        _target = mechFactory.Create(targetData);
        _player2.AddUnit(_target);
    }

    private PhysicalAttackCommand CreateCommand(PhysicalAttackDeclarationData? declaration) => new()
    {
        GameOriginId = Guid.NewGuid(),
        PlayerId = _player1.Id,
        UnitId = _attacker.Id,
        Declaration = declaration
    };

    private PhysicalAttackDeclarationData Kick(Guid targetId) => new()
    {
        AttackType = PhysicalAttackType.Kick,
        AttackerLimbs = [PartLocation.LeftLeg],
        TargetId = targetId
    };

    [Fact]
    public void Render_Declaration_NamesTheAttackAndTheTarget()
    {
        // Act
        var result = CreateCommand(Kick(_target.Id)).Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"Player 1's {_attacker.Model} declares Kick against {_target.Model}");
    }

    [Fact]
    public void Render_NoDeclaration_SaysNoPhysicalAttack()
    {
        // Act
        var result = CreateCommand(null).Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"Player 1's {_attacker.Model} declares no physical attack");
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenUnitNotFound()
    {
        // Act
        var result = (CreateCommand(null) with { UnitId = Guid.NewGuid() }).Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenTargetNotFound()
    {
        // Act
        var result = CreateCommand(Kick(Guid.NewGuid())).Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
