using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class PhysicalAttackCommandTests
{
    // The real service is used rather than a substitute: when Command_PhysicalAttack was
    // missing, GetString returned the key itself and a stubbed service would have hidden it.
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Player _attackingPlayer = new(Guid.NewGuid(), "Player 1", PlayerControlType.Human);
    private readonly Player _defendingPlayer = new(Guid.NewGuid(), "Player 2", PlayerControlType.Human);
    private readonly Unit _attacker;
    private readonly Unit _target;

    public PhysicalAttackCommandTests()
    {
        _game.Players.Returns([_attackingPlayer, _defendingPlayer]);

        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);

        var attackerData = MechFactoryTests.CreateDummyMechData();
        attackerData.Id = Guid.NewGuid();
        _attacker = mechFactory.Create(attackerData);
        _attackingPlayer.AddUnit(_attacker);

        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        _target = mechFactory.Create(targetData);
        _defendingPlayer.AddUnit(_target);
    }

    private PhysicalAttackCommand CreateCommand(PhysicalAttackType attackType = PhysicalAttackType.Punch)
        => new()
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = _attackingPlayer.Id,
            UnitId = _attacker.Id,
            TargetUnitId = _target.Id,
            AttackType = attackType
        };

    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    [InlineData(PhysicalAttackType.Push)]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void Render_DescribesTheDeclaration_ForEveryAttackType(PhysicalAttackType attackType)
    {
        var command = CreateCommand(attackType);

        var result = command.Render(_localizationService, _game);

        result.ShouldBe(
            $"{_attackingPlayer.Name}'s {_attacker.Model} declares a {attackType} against {_target.Model}");
    }

    [Fact]
    public void Render_ResolvesTheLocalizationKey_RatherThanEchoingIt()
    {
        // Regression: Command_PhysicalAttack was absent from the localization service, so
        // GetString returned the key. The key carries no placeholders, so string.Format
        // discarded every argument and the combat log showed the literal text
        // "Command_PhysicalAttack" for every physical attack.
        var command = CreateCommand();

        var result = command.Render(_localizationService, _game);

        result.ShouldNotBe("Command_PhysicalAttack");
        result.ShouldContain(_attackingPlayer.Name);
        result.ShouldContain(_attacker.Model);
        result.ShouldContain(_target.Model);
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenThePlayerIsNotInTheGame()
    {
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        command.Render(_localizationService, _game).ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenTheAttackingUnitCannotBeFound()
    {
        var command = CreateCommand() with { UnitId = Guid.NewGuid() };

        command.Render(_localizationService, _game).ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenTheTargetCannotBeFound()
    {
        var command = CreateCommand() with { TargetUnitId = Guid.NewGuid() };

        command.Render(_localizationService, _game).ShouldBeEmpty();
    }

    [Fact]
    public void Render_ResolvesTargetsAcrossAllPlayers()
    {
        // The command does not restrict the target to an opposing player; whether a
        // friendly declaration is legal is a validation concern, not a rendering one.
        var friendly = MechFactoryTests.CreateDummyMechData();
        friendly.Id = Guid.NewGuid();
        var friendlyUnit = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService).Create(friendly);
        _attackingPlayer.AddUnit(friendlyUnit);

        var command = CreateCommand() with { TargetUnitId = friendlyUnit.Id };

        command.Render(_localizationService, _game)
            .ShouldBe($"{_attackingPlayer.Name}'s {_attacker.Model} declares a Punch against {friendlyUnit.Model}");
    }
}
