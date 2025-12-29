using NSubstitute;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class WeaponsEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly ITacticalEvaluator _tacticalEvaluator;
    private readonly IPlayer _player;
    private readonly WeaponsEngine _sut;

    public WeaponsEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _tacticalEvaluator = Substitute.For<ITacticalEvaluator>();
        _player = Substitute.For<IPlayer>();

        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");

        _sut = new WeaponsEngine(_clientGame, _tacticalEvaluator);
    }

    [Fact]
    public async Task MakeDecision_NoUnitCanFire_ShouldSkipTurnAndDeclareEmptyAttack()
    {
        var unit = CreateMockUnit(canFireWeapons: false, hasDeclaredWeaponAttack: false, position: null);
        _player.AliveUnits.Returns([unit]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.GameOriginId.ShouldBe(_clientGame.Id);
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(unit.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MakeDecision_WhenAttackerHasNoPosition_ShouldSkipTurnAndDeclareEmptyAttack()
    {
        var unit = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false, position: null);
        _player.AliveUnits.Returns([unit]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(unit.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MakeDecision_WhenNoValidTargets_ShouldDeclareEmptyAttackWithAttacker()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _player.AliveUnits.Returns([attacker]);

        _clientGame.Players.Returns([_player]);
        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(attacker.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MakeDecision_WhenBestTargetNotFoundInEnemies_ShouldDeclareEmptyAttack()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _player.AliveUnits.Returns([attacker]);

        var enemy = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 4), HexDirection.Top),
            isDeployed: true,
            name: "Enemy");
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemy]);

        _clientGame.Players.Returns([_player, enemyPlayer]);

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = Guid.NewGuid(),
                    Score = 10,
                    ViableWeapons = []
                }
            ]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(attacker.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MakeDecision_WhenNoWeaponsMeetThreshold_ShouldDeclareEmptyAttack()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _player.AliveUnits.Returns([attacker]);

        var enemy = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 4), HexDirection.Top),
            isDeployed: true,
            name: "Enemy");
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemy]);

        _clientGame.Players.Returns([_player, enemyPlayer]);

        var enemyId = enemy.Id;

        var weapon = new TestWeapon(new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = weapon, HitProbability = WeaponsEngine.HitProbabilityThreshold - 0.01 }
                    ]
                }
            ]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(attacker.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MakeDecision_WhenWeaponsMeetThreshold_ShouldDeclareWeaponAttackWithSelectedWeapons()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _player.AliveUnits.Returns([attacker]);

        var enemy = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 4), HexDirection.Top),
            isDeployed: true,
            name: "Enemy");
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemy]);

        _clientGame.Players.Returns([_player, enemyPlayer]);

        var enemyId = enemy.Id;

        var belowThresholdWeapon = new TestWeapon(new WeaponDefinition("Below", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var atThresholdWeapon = new TestWeapon(new WeaponDefinition("At", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var aboveThresholdWeapon = new TestWeapon(new WeaponDefinition("Above", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = belowThresholdWeapon, HitProbability = 0.2 },
                        new WeaponEvaluationData { Weapon = atThresholdWeapon, HitProbability = 0.3 },
                        new WeaponEvaluationData { Weapon = aboveThresholdWeapon, HitProbability = 0.8 },
                    ]
                }
            ]);

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(attacker.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(2);
        capturedCommand.WeaponTargets.All(wt => wt.TargetId == enemy.Id).ShouldBeTrue();
        capturedCommand.WeaponTargets.All(wt => wt.IsPrimaryTarget).ShouldBeTrue();
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["At", "Above"]);
    }

    [Fact]
    public async Task MakeDecision_WhenSkipTurnHasNoUnits_ShouldRethrowBotDecisionException()
    {
        _player.AliveUnits.Returns([]);
        _clientGame.Players.Returns([_player]);

        var exception = await Should.ThrowAsync<BotDecisionException>(async () =>
        {
            await _sut.MakeDecision(_player);
        });

        exception.DecisionEngineType.ShouldBe(nameof(WeaponsEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenUnexpectedException_ShouldSkipTurn()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _player.AliveUnits.Returns([attacker]);

        _clientGame.Players.Returns([_player]);

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(_ => throw new InvalidOperationException("Unexpected"));

        WeaponAttackDeclarationCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.DeclareWeaponAttack(Arg.Do<WeaponAttackDeclarationCommand>(cmd =>
        {
            capturedCommand = cmd;
            commandCaptured = true;
        }));

        await _sut.MakeDecision(_player);

        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(attacker.Id);
        capturedCommand.WeaponTargets.Count.ShouldBe(0);
    }

    private static IUnit CreateMockUnit(
        bool canFireWeapons,
        bool hasDeclaredWeaponAttack,
        HexPosition? position,
        bool isDeployed = true,
        Guid? id = null,
        string? name = null)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(id ?? Guid.NewGuid());
        unit.Name.Returns(name ?? "Unit");
        unit.CanFireWeapons.Returns(canFireWeapons);
        unit.HasDeclaredWeaponAttack.Returns(hasDeclaredWeaponAttack);
        unit.IsDeployed.Returns(isDeployed);
        unit.Position.Returns(position);
        unit.MovementTaken.Returns((MovementPath?)null);
        return unit;
    }

    private class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}
