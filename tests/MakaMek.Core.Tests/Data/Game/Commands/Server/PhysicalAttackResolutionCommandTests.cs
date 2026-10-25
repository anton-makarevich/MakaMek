using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class PhysicalAttackResolutionCommandTests
{
    private readonly ILocalizationService _localizationService = TestLocalizationService.Create();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Player _player1 = new(Guid.NewGuid(), "Player 1", PlayerControlType.Human);
    private readonly Unit _attacker;
    private readonly Unit _target;

    public PhysicalAttackResolutionCommandTests()
    {
        var player2 = new Player(Guid.NewGuid(), "Player 2", PlayerControlType.Human);
        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
        var attackerData = MechFactoryTests.CreateDummyMechData();
        attackerData.Id = Guid.NewGuid();
        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        _attacker = mechFactory.Create(attackerData);
        _target = mechFactory.Create(targetData);
        _player1.AddUnit(_attacker);
        player2.AddUnit(_target);
        _game.Players.Returns(new List<IPlayer> { _player1, player2 });
    }

    [Fact]
    public void Render_Hit_NamesTheAttackAndLimbAndListsTheDamage()
    {
        // Arrange
        var sut = CreateCommand(new AttackResolutionData(
            5,
            [new DiceResult(4), new DiceResult(4)],
            true,
            HitDirection.Front,
            0,
            new AttackHitLocationsData(
                [new LocationHitData([new LocationDamageData(PartLocation.CenterTorso, 2, 0, false)], [], [3, 4],
                    PartLocation.CenterTorso)],
                2, [], 1)));

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("Player 1's LCT-1V hits Player 2's LCT-1V with Punch (Left Arm) (Target: 5, Roll: 8)");
        result.ShouldContain("Attack Direction: Front");
        result.ShouldContain("Total Damage: 2");
        result.ShouldContain("CT (Roll: 7): 2 armor damage");
    }

    [Fact]
    public void Render_Miss_IsOneLine()
    {
        // Arrange
        var sut = CreateCommand(new AttackResolutionData(
            9, [new DiceResult(2), new DiceResult(3)], false, HitDirection.Front, 0));

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Player 1's LCT-1V misses Player 2's LCT-1V with Punch (Left Arm) (Target: 9, Roll: 5)");
    }

    [Fact]
    public void Render_UsesLocalizedAttackTypeAndLimb()
    {
        // Arrange
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString("Command_WeaponAttackResolution_Miss").Returns("{2}");
        localizationService.GetString("PhysicalAttackType_Punch").Returns("Coup de poing");
        localizationService.GetString("MechPart_LeftArm").Returns("Bras gauche");
        var sut = CreateCommand(new AttackResolutionData(
            9, [new DiceResult(2), new DiceResult(3)], false, HitDirection.Front, 0));

        // Act
        var result = sut.Render(localizationService, _game);

        // Assert
        result.ShouldBe("Coup de poing (Bras gauche)");
    }

    [Fact]
    public void Render_DestroyedPartsAndUnit_AreListed()
    {
        // Arrange
        var sut = CreateCommand(new AttackResolutionData(
            5,
            [new DiceResult(6), new DiceResult(6)],
            true,
            HitDirection.Front,
            0,
            new AttackHitLocationsData(
                [new LocationHitData([new LocationDamageData(PartLocation.Head, 3, 3, true)], [], [6, 6],
                    PartLocation.Head)],
                6, [], 1),
            [PartLocation.Head],
            true));

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("Destroyed parts:");
        result.ShouldContain("- Head destroyed");
        result.ShouldContain("LCT-1V has been destroyed!");
    }

    [Fact]
    public void Render_UnknownAttacker_IsEmpty()
    {
        // Arrange
        var sut = CreateCommand(new AttackResolutionData(
            9, [new DiceResult(2), new DiceResult(3)], false, HitDirection.Front, 0)) with { AttackerId = Guid.NewGuid() };

        // Act + Assert
        sut.Render(_localizationService, _game).ShouldBeEmpty();
    }

    private PhysicalAttackResolutionCommand CreateCommand(AttackResolutionData resolution) => new()
    {
        ResolutionId = Guid.NewGuid(),
        GameOriginId = Guid.NewGuid(),
        PlayerId = _player1.Id,
        AttackerId = _attacker.Id,
        TargetId = _target.Id,
        ResolutionData = new PhysicalAttackResolutionData(
            PhysicalAttackType.Punch, PartLocation.LeftArm, 2, resolution, [])
    };
}
