using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public sealed class PhysicalAttackStateTests
{
    private readonly PhysicalAttackState _sut;
    private readonly ClientGame _game;
    private readonly BattleMapViewModel _viewModel;
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private Player _localPlayer = null!;
    private Unit _localUnit = null!;
    private Unit _enemyUnit = null!;
    private readonly ILocalizationService _localization = new FakeLocalizationService();

    public PhysicalAttackStateTests()
    {
        var rules = new TotalWarfareRulesProvider();
        var mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            _localization);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _localUnit = mechFactory.Create(unitData with { Id = Guid.NewGuid() });
        _enemyUnit = mechFactory.Create(unitData with { Id = Guid.NewGuid() });

        var localPlayer = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human);
        var enemyPlayer = new Player(Guid.NewGuid(), "Enemy", PlayerControlType.Human);

        _game = new ClientGame(
            rules,
            mechFactory,
            _commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            new BattleMapFactory(),
            Substitute.For<IHashService>(),
            Substitute.For<ILogger<ClientGame>>());

        _viewModel = new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localization,
            Substitute.For<IDispatcherService>(),
            rules,
            Substitute.For<IPlatformService>());
        _viewModel.Game = _game;

        var map = new BattleMapFactory().GenerateMap(
            5,
            5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        _game.SetBattleMap(map);
        _game.JoinGameWithUnits(localPlayer, [], []).GetAwaiter().GetResult();

        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer.Id,
            PlayerName = localPlayer.Name,
            Tint = "#FF0000",
            Units = [unitData with { Id = _localUnit.Id }],
            PilotAssignments = []
        });
        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = enemyPlayer.Id,
            PlayerName = enemyPlayer.Name,
            Tint = "#0000FF",
            Units = [unitData with { Id = _enemyUnit.Id }],
            PilotAssignments = []
        });

        _localPlayer = _game.Players.Single(player => player.Id == localPlayer.Id) as Player
            ?? throw new InvalidOperationException("The local player was not created.");
        _localUnit = _localPlayer.Units.Single() as Unit
            ?? throw new InvalidOperationException("The local unit was not created.");
        _enemyUnit = _game.Players.Single(player => player.Id == enemyPlayer.Id).Units.Single() as Unit
            ?? throw new InvalidOperationException("The enemy unit was not created.");

        Deploy(_localPlayer.Id, _localUnit.Id, new HexCoordinateData(2, 2));
        Deploy(enemyPlayer.Id, _enemyUnit.Id, new HexCoordinateData(2, 3));
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.PhysicalAttack
        });
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = _localPlayer.Id,
            UnitsToPlay = 1
        });

        _sut = new PhysicalAttackState(_viewModel);
    }

    [Fact]
    public void ExecutePlayerAction_AfterSelectingAdjacentTarget_DeclaresPunch()
    {
        _sut.HandleUnitSelectionFromList(_localUnit);
        _sut.HandleHexSelection(_game.BattleMap!.GetHex(_enemyUnit.Position!.Coordinates)!);

        _sut.PlayerActionLabel.ShouldBe(nameof(PhysicalAttackType.Punch));
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(
            Arg.Is<PhysicalAttackCommand>(command =>
                command.UnitId == _localUnit.Id &&
                command.TargetUnitId == _enemyUnit.Id &&
                command.AttackType == PhysicalAttackType.Punch));
    }

    [Fact]
    public void GetAvailableActions_WithAdjacentTarget_ProvidesExplicitKickAction()
    {
        _sut.HandleUnitSelectionFromList(_localUnit);
        _sut.HandleHexSelection(_game.BattleMap!.GetHex(_enemyUnit.Position!.Coordinates)!);

        var kick = _sut.GetAvailableActions().Single(action => action.Label == "Kick");
        kick.OnExecute();

        _commandPublisher.Received(1).PublishCommand(
            Arg.Is<PhysicalAttackCommand>(command =>
                command.AttackType == PhysicalAttackType.Kick &&
                command.TargetUnitId == _enemyUnit.Id));
    }

    [Fact]
    public void GetAvailableActions_WithAdjacentTarget_ProvidesExplicitPushAction()
    {
        _sut.HandleUnitSelectionFromList(_localUnit);
        _sut.HandleHexSelection(_game.BattleMap!.GetHex(_enemyUnit.Position!.Coordinates)!);

        var push = _sut.GetAvailableActions().Single(action => action.Label == "Push");
        push.OnExecute();

        _commandPublisher.Received(1).PublishCommand(
            Arg.Is<PhysicalAttackCommand>(command =>
                command.AttackType == PhysicalAttackType.Push &&
                command.TargetUnitId == _enemyUnit.Id));
    }

    [Fact]
    public void GetAvailableActions_WithoutTarget_ProvidesPassActionOnly()
    {
        _sut.HandleUnitSelectionFromList(_localUnit);

        var actions = _sut.GetAvailableActions().ToList();

        actions.Count.ShouldBe(1);
        actions[0].Label.ShouldBe("Pass physical attack");
        actions[0].OnExecute();

        _commandPublisher.Received(1).PublishCommand(
            Arg.Is<PassPhysicalAttackCommand>(command =>
                command.UnitId == _localUnit.Id &&
                command.PlayerId == _localPlayer.Id));
    }

    private void Deploy(Guid playerId, Guid unitId, HexCoordinateData coordinates)
    {
        _game.HandleCommand(new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitId = unitId,
            Position = coordinates,
            Direction = 0
        });
    }
}
