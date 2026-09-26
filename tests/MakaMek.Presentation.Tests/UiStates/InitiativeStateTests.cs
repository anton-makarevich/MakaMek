using System.Reactive.Concurrency;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class InitiativeStateTests
{
    private readonly InitiativeState _sut;
    private readonly ClientGame _game;
    private readonly Player _humanPlayer;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly Guid _idempotencyKey = Guid.NewGuid();
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    public InitiativeStateTests()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString("Initiative_WaitingLabel").Returns("Waiting for other players");

        var dispatcherService = Substitute.For<IDispatcherService>();
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);

        _battleMapViewModel = new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            localizationService,
            dispatcherService,
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>());

        var rules = new TotalWarfareRulesProvider();
        var hashService = Substitute.For<IHashService>();
        hashService.ComputeCommandIdempotencyKey(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Type>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<string?>())
            .Returns(_idempotencyKey);

        _game = new ClientGame(rules,
            new MechFactory(rules, new ClassicBattletechComponentProvider(), localizationService),
            _commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            Substitute.For<IBattleMapFactory>(),
            hashService,
            Substitute.For<ILogger<ClientGame>>());

        _humanPlayer = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _game.JoinGameWithUnits(_humanPlayer, [], []);
        _game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = _humanPlayer.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _humanPlayer.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });

        _battleMapViewModel.Game = _game;
        SetPhase(PhaseNames.Initiative);

        _sut = new InitiativeState(_battleMapViewModel);
        BindViewModelCurrentStateTo(_sut, _battleMapViewModel);
    }

    private void SetPhase(PhaseNames phase)
        => _game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = phase });

    private void SetActivePlayer(Guid playerId)
        => _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitsToPlay = 0
        });

    private static void BindViewModelCurrentStateTo(InitiativeState state, BattleMapViewModel viewModel)
        => typeof(BattleMapViewModel).GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .GetSetMethod(true)!.Invoke(viewModel, [state]);

    [Fact]
    public void Game_IsTakenFromTheViewModel()
        => _sut.Game.ShouldBe(_game);

    [Fact]
    public void WhenItIsNotOurTurn_NoActionIsOffered()
    {
        _sut.IsActionRequired.ShouldBeFalse();
        _sut.ActionLabel.ShouldBe("Waiting for other players");
        ((IUiState)_sut).CanExecutePlayerAction.ShouldBeFalse();
        ((IUiState)_sut).GetAvailableActions().ShouldBeEmpty();
    }

    [Fact]
    public void WhenItIsOurTurn_StillOffersNoAction_BecauseTheServerRolls()
    {
        SetActivePlayer(_humanPlayer.Id);

        // The server rolls initiative for every player, so being the active player changes nothing
        // here: the state reports the phase, it does not ask for input.
        _sut.IsActionRequired.ShouldBeFalse();
        _sut.ActionLabel.ShouldBe("Waiting for other players");
        ((IUiState)_sut).CanExecutePlayerAction.ShouldBeFalse();
        ((IUiState)_sut).GetAvailableActions().ShouldBeEmpty();
    }

    [Fact]
    public void ExecutePlayerAction_PublishesNothing_EvenWhenItIsOurTurn()
    {
        SetActivePlayer(_humanPlayer.Id);
        _commandPublisher.ClearReceivedCalls();

        ((IUiState)_sut).ExecutePlayerAction();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<RollDiceCommand>());
    }

    [Fact]
    public void SelectionHandlers_AreInert()
    {
        // Initiative is resolved server-side; map and list interaction must not change state.
        SetActivePlayer(_humanPlayer.Id);
        var unit = _battleMapViewModel.Units.First();

        Should.NotThrow(() => _sut.HandleUnitSelectionFromList(unit));
        Should.NotThrow(() => _sut.HandleUnitSelectionFromList(null));
        Should.NotThrow(() => _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1))));
        Should.NotThrow(() => _sut.HandleFacingSelection(HexDirection.Top));

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<RollDiceCommand>());
    }
}
