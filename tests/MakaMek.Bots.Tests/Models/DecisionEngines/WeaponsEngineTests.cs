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
                    ViableWeapons = [],
                    ConfigurationScores = []
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
    public async Task MakeDecision_WhenAllWeaponsHaveZeroHitProbability_ShouldDeclareEmptyAttack()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(0);
        attacker.HeatDissipation.Returns(0);
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
                        new WeaponEvaluationData { Weapon = weapon, HitProbability = 0, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } }
                    ],
                    ConfigurationScores = []
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
    public async Task MakeDecision_WhenWeaponsFitHeatBudget_ShouldSelectWeaponsSortedByProbabilityThenDamage()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(0);
        attacker.HeatDissipation.Returns(0);
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

        var highProbHighDamage = new TestWeapon(new WeaponDefinition("HighProbHighDamage", 10, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var highProbLowDamage = new TestWeapon(new WeaponDefinition("HighProbLowDamage", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var midProb = new TestWeapon(new WeaponDefinition("MidProb", 20, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var zeroProb = new TestWeapon(new WeaponDefinition("ZeroProb", 999, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = midProb, HitProbability = 0.6, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = highProbLowDamage, HitProbability = 0.9, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = zeroProb, HitProbability = 0, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = highProbHighDamage, HitProbability = 0.9, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                    ],
                    ConfigurationScores = []
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
        capturedCommand.WeaponTargets.Count.ShouldBe(3);
        capturedCommand.WeaponTargets.All(wt => wt.TargetId == enemy.Id).ShouldBeTrue();
        capturedCommand.WeaponTargets.All(wt => wt.IsPrimaryTarget).ShouldBeTrue();
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["HighProbHighDamage", "HighProbLowDamage", "MidProb"]);
    }

    [Fact]
    public async Task MakeDecision_WhenSomeWeaponsExceedHeatBudget_ShouldSelectOnlyWeaponsWithinBudget()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(3);
        attacker.HeatDissipation.Returns(1);
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

        var lowHeat = new TestWeapon(new WeaponDefinition("LowHeat", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var someHeat = new TestWeapon(new WeaponDefinition("SomeHeat", 5, 2, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));
        var tooMuchHeat = new TestWeapon(new WeaponDefinition("TooMuchHeat", 5, 10, 0, 3, 6, 9, WeaponType.Energy, 100,
            WeaponComponentType: MakaMekComponent.MachineGun));

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = tooMuchHeat, HitProbability = 0.9, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = lowHeat, HitProbability = 0.8, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = someHeat, HitProbability = 0.7, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                    ],
                    ConfigurationScores = []
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
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["LowHeat","SomeHeat"]);
    }

    [Fact]
    public async Task MakeDecision_WhenAmmoIsLowAndHitProbabilityNotEnough_ShouldSkipAmmoWeapon()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(0);
        attacker.HeatDissipation.Returns(0);
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

        var ammoWeapon = new TestWeapon(new WeaponDefinition(
            "AmmoWeapon",
            10,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Ballistic,
            100,
            WeaponComponentType: MakaMekComponent.AC5,
            AmmoComponentType: MakaMekComponent.ISAmmoAC5));

        var energyWeapon = new TestWeapon(new WeaponDefinition(
            "EnergyWeapon",
            5,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Energy,
            100,
            WeaponComponentType: MakaMekComponent.MediumLaser));

        attacker.GetRemainingAmmoShots(ammoWeapon).Returns(1);

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = ammoWeapon, HitProbability = 0.7, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = energyWeapon, HitProbability = 0.6, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                    ],
                    ConfigurationScores = []
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
        capturedCommand.WeaponTargets.Count.ShouldBe(1);
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["EnergyWeapon"]);
    }

    [Fact]
    public async Task MakeDecision_WhenAmmoIsLowButHitProbabilityHigh_ShouldIncludeAmmoWeapon()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(0);
        attacker.HeatDissipation.Returns(0);
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

        var ammoWeapon = new TestWeapon(new WeaponDefinition(
            "AmmoWeapon",
            10,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Ballistic,
            100,
            WeaponComponentType: MakaMekComponent.AC5,
            AmmoComponentType: MakaMekComponent.ISAmmoAC5));

        var energyWeapon = new TestWeapon(new WeaponDefinition(
            "EnergyWeapon",
            5,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Energy,
            100,
            WeaponComponentType: MakaMekComponent.MediumLaser));

        attacker.GetRemainingAmmoShots(ammoWeapon).Returns(1);

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = ammoWeapon, HitProbability = 0.8, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = energyWeapon, HitProbability = 0.6, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                    ],
                    ConfigurationScores = []
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
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["AmmoWeapon", "EnergyWeapon"]);
    }

    [Fact]
    public async Task MakeDecision_WhenHitProbabilitySame_ShouldPreferAmmoWeaponWithMoreRemainingShots()
    {
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false,
            position: new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.GetProjectedHeatValue(_clientGame.RulesProvider).Returns(0);
        attacker.HeatDissipation.Returns(0);
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

        var lowAmmoWeapon = new TestWeapon(new WeaponDefinition(
            "LowAmmoWeapon",
            10,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Ballistic,
            100,
            WeaponComponentType: MakaMekComponent.AC5,
            AmmoComponentType: MakaMekComponent.ISAmmoAC5));

        var highAmmoWeapon = new TestWeapon(new WeaponDefinition(
            "HighAmmoWeapon",
            10,
            0,
            0,
            3,
            6,
            9,
            WeaponType.Ballistic,
            100,
            WeaponComponentType: MakaMekComponent.AC5,
            AmmoComponentType: MakaMekComponent.ISAmmoAC5));

        attacker.GetRemainingAmmoShots(lowAmmoWeapon).Returns(1);
        attacker.GetRemainingAmmoShots(highAmmoWeapon).Returns(20);

        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([
                new TargetScore
                {
                    TargetId = enemyId,
                    Score = 10,
                    ViableWeapons =
                    [
                        new WeaponEvaluationData { Weapon = lowAmmoWeapon, HitProbability = 0.3, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                        new WeaponEvaluationData { Weapon = highAmmoWeapon, HitProbability = 0.3, Configuration = new WeaponConfiguration { Type = WeaponConfigurationType.None, Value = 0 } },
                    ],
                    ConfigurationScores = []
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
        capturedCommand.WeaponTargets.Count.ShouldBe(1);
        capturedCommand.WeaponTargets.Select(wt => wt.Weapon.Name).ShouldBe(["HighAmmoWeapon"]);
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
            .Returns<ValueTask<IReadOnlyList<TargetScore>>>(_ => throw new InvalidOperationException("Unexpected"));

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
    public async Task MakeDecision_WhenAttackerHasMovementPath_ShouldUseActualPath()
    {
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var pathPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Top);
        var movementPath = MovementPath.CreateStandingStillPath(pathPosition); 
        var attacker = CreateMockUnit(canFireWeapons: true, hasDeclaredWeaponAttack: false, position: position);
        attacker.MovementTaken.Returns(movementPath);
    
        _player.AliveUnits.Returns([attacker]);
        _clientGame.Players.Returns([_player]);
        _tacticalEvaluator.EvaluateTargets(attacker, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns([]);
    
        await _sut.MakeDecision(_player);
    
        await _tacticalEvaluator.Received(1).EvaluateTargets(
            attacker, 
            Arg.Is<MovementPath>(p => p == movementPath), 
            Arg.Any<IReadOnlyList<IUnit>>());
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
