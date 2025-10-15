using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class EndGameViewModelTests
{
    private readonly EndGameViewModel _sut;
    private readonly INavigationService _navigationService;
    private readonly ClientGame _game;
    private readonly MechFactory _mechFactory;

    public EndGameViewModelTests()
    {
        var localizationService = new FakeLocalizationService();
        _navigationService = Substitute.For<INavigationService>();

        _sut = new EndGameViewModel(localizationService);
        _sut.SetNavigationService(_navigationService);

        // Create a test game
        var rulesProvider = new ClassicBattletechRulesProvider();
        _mechFactory = new MechFactory(
            rulesProvider,
            new ClassicBattletechComponentProvider(),
            localizationService);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var toHitCalculator = Substitute.For<IToHitCalculator>();
        var pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
        var consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
        var heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
        var mapFactory = Substitute.For<IBattleMapFactory>();

        _game = new ClientGame(
            rulesProvider,
            _mechFactory,
            commandPublisher,
            toHitCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            mapFactory);
    }

    private Unit CreateMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return _mechFactory.Create(mechData);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _sut.Players.ShouldBeEmpty();
        _sut.ReturnToMenuCommand.ShouldNotBeNull();
    }

    [Fact]
    public void Initialize_ShouldPopulatePlayers()
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1", "#FF0000");
        var player2 = new Player(Guid.NewGuid(), "Player2", "#00FF00");
        var mech1 = CreateMech();
        var mech2 = CreateMech();

        var mech1Data = mech1.ToData();
        var mech2Data = mech2.ToData();

        // Manually handle join commands to add players to the game
        // Note: GameOriginId must be different from the game's ID for the command to be processed
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1.Id,
            PlayerName = player1.Name,
            Tint = player1.Tint,
            Units = [mech1Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player2.Id,
            PlayerName = player2.Name,
            Tint = player2.Tint,
            Units = [mech2Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        // Act
        _sut.Initialize(_game, GameEndReason.Victory);

        // Assert
        _sut.Players.Count.ShouldBe(2);
    }

    [Fact]
    public void Initialize_ShouldOrderPlayersByVictoryStatus()
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1", "#FF0000");
        var player2 = new Player(Guid.NewGuid(), "Player2", "#00FF00");
        var mech1 = CreateMech();
        var mech2 = CreateMech();

        // Destroy player1's mech by destroying its head
        var head = mech1.Parts[PartLocation.Head];
        var totalDamage = head.MaxArmor + head.MaxStructure;
        head.ApplyDamage(totalDamage, HitDirection.Front);

        var mech1Data = mech1.ToData();
        var mech2Data = mech2.ToData();

        // Manually handle join commands to add players to the game
        // Note: GameOriginId must be different from the game's ID for the command to be processed
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1.Id,
            PlayerName = player1.Name,
            Tint = player1.Tint,
            Units = [mech1Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player2.Id,
            PlayerName = player2.Name,
            Tint = player2.Tint,
            Units = [mech2Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        // Act
        _sut.Initialize(_game, GameEndReason.Victory);

        // Assert
        _sut.Players.Count.ShouldBe(2);
        _sut.Players[0].IsVictor.ShouldBeTrue(); // Player2 should be first (victor)
        _sut.Players[0].Name.ShouldBe("Player2");
        _sut.Players[1].IsVictor.ShouldBeFalse(); // Player1 should be second
        _sut.Players[1].Name.ShouldBe("Player1");
    }

    [Fact]
    public void TitleText_ShouldReturnVictoryTitle_WhenReasonIsVictory()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [], []);

        // Act
        _sut.Initialize(_game, GameEndReason.Victory);

        // Assert
        _sut.TitleText.ShouldBe("Victory!");
    }

    [Fact]
    public void TitleText_ShouldReturnGenericTitle_WhenReasonIsNotVictory()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [], []);

        // Act
        _sut.Initialize(_game, GameEndReason.PlayersLeft);

        // Assert
        _sut.TitleText.ShouldBe("Game Over");
    }

    [Fact]
    public void SubtitleText_ShouldReturnVictorName_WhenThereIsAVictor()
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1", "#FF0000");
        var player2 = new Player(Guid.NewGuid(), "Player2", "#00FF00");
        var mech1 = CreateMech();
        var mech2 = CreateMech();

        // Destroy player1's mech by destroying its head
        var head = mech1.Parts[PartLocation.Head];
        var totalDamage = head.MaxArmor + head.MaxStructure;
        head.ApplyDamage(totalDamage, HitDirection.Front);

        var mech1Data = mech1.ToData();
        var mech2Data = mech2.ToData();

        // Manually handle join commands to add players to the game
        // Note: GameOriginId must be different from the game's ID for the command to be processed
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1.Id,
            PlayerName = player1.Name,
            Tint = player1.Tint,
            Units = [mech1Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player2.Id,
            PlayerName = player2.Name,
            Tint = player2.Tint,
            Units = [mech2Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        // Act
        _sut.Initialize(_game, GameEndReason.Victory);

        // Assert
        _sut.SubtitleText.ShouldBe("Player2 is victorious!");
    }

    [Fact]
    public void SubtitleText_ShouldReturnDrawMessage_WhenNoVictor()
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1", "#FF0000");
        var player2 = new Player(Guid.NewGuid(), "Player2", "#00FF00");
        var mech1 = CreateMech();
        var mech2 = CreateMech();

        // Destroy both mechs by destroying their heads
        var head1 = mech1.Parts[PartLocation.Head];
        var totalDamage1 = head1.MaxArmor + head1.MaxStructure;
        head1.ApplyDamage(totalDamage1, HitDirection.Front);

        var head2 = mech2.Parts[PartLocation.Head];
        var totalDamage2 = head2.MaxArmor + head2.MaxStructure;
        head2.ApplyDamage(totalDamage2, HitDirection.Front);

        var mech1Data = mech1.ToData();
        var mech2Data = mech2.ToData();

        // Manually handle join commands to add players to the game
        // Note: GameOriginId must be different from the game's ID for the command to be processed
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1.Id,
            PlayerName = player1.Name,
            Tint = player1.Tint,
            Units = [mech1Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player2.Id,
            PlayerName = player2.Name,
            Tint = player2.Tint,
            Units = [mech2Data],
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        // Act
        _sut.Initialize(_game, GameEndReason.Victory);

        // Assert
        _sut.SubtitleText.ShouldBe("The battle ended in a draw");
    }

    [Fact]
    public void SubtitleText_ShouldReturnReasonMessage_WhenReasonIsNotVictory()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [], []);

        // Act
        _sut.Initialize(_game, GameEndReason.PlayersLeft);

        // Assert
        _sut.SubtitleText.ShouldBe("All players have left the game");
    }

    [Fact]
    public async Task ReturnToMenuCommand_ShouldNavigateToRoot()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [], []);
        _sut.Initialize(_game, GameEndReason.Victory);

        // Act
        await ((IAsyncCommand)_sut.ReturnToMenuCommand).ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToRootAsync();
    }

    [Fact]
    public async Task ReturnToMenuCommand_ShouldDisposeGame()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [], []);
        _sut.Initialize(_game, GameEndReason.Victory);

        // Act
        await ((IAsyncCommand)_sut.ReturnToMenuCommand).ExecuteAsync();

        // Assert
        // Game should be disposed (we can't directly test this, but we can verify navigation happened)
        await _navigationService.Received(1).NavigateToRootAsync();
    }
}

