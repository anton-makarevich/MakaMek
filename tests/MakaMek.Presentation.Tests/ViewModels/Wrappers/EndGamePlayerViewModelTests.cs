using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class EndGamePlayerViewModelTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly MechFactory _mechFactory;

    public EndGamePlayerViewModelTests()
    {
        _mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
    }

    private Unit CreateMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return _mechFactory.Create(mechData);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local, "#FF0000");
        var mech = CreateMech();
        player.AddUnit(mech);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: true, _localizationService);

        // Assert
        sut.Name.ShouldBe("TestPlayer");
        sut.Tint.ShouldBe("#FF0000");
        sut.IsVictor.ShouldBeTrue();
        sut.Units.Count.ShouldBe(1);
    }

    [Fact]
    public void Constructor_ShouldCreateUnitViewModels()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local);
        var mech1 = CreateMech();
        var mech2 = CreateMech();
        player.AddUnit(mech1);
        player.AddUnit(mech2);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: false, _localizationService);

        // Assert
        sut.Units.Count.ShouldBe(2);
        sut.Units[0].ShouldBeOfType<EndGameUnitViewModel>();
        sut.Units[1].ShouldBeOfType<EndGameUnitViewModel>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsVictor_ShouldReflectConstructorParameter(bool isVictor)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor, _localizationService);

        // Assert
        sut.IsVictor.ShouldBe(isVictor);
    }

    [Fact]
    public void Name_ShouldReturnPlayerName()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "VictoriousPlayer", PlayerControlType.Local);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: true, _localizationService);

        // Assert
        sut.Name.ShouldBe("VictoriousPlayer");
    }

    [Fact]
    public void Tint_ShouldReturnPlayerTint()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local, "#00FF00");

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: false, _localizationService);

        // Assert
        sut.Tint.ShouldBe("#00FF00");
    }

    [Fact]
    public void Units_ShouldBeEmpty_WhenPlayerHasNoUnits()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: false, _localizationService);

        // Assert
        sut.Units.ShouldBeEmpty();
    }
    
    [Fact]
    public void VictorBadgeText_ShouldReturnLocalizedText_WhenIsVictor()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local);
        _localizationService.GetString("EndGame_Victor_Badge").Returns("Victor");

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: true, _localizationService);

        // Assert
        sut.VictorBadgeText.ShouldBe("Victor");
    }
    
    [Fact]
    public void VictorBadgeText_ShouldBeEmpty_WhenNotVictor()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "TestPlayer", PlayerControlType.Local);

        // Act
        var sut = new EndGamePlayerViewModel(player, isVictor: false, _localizationService);

        // Assert
        sut.VictorBadgeText.ShouldBeEmpty();
        _localizationService.DidNotReceive().GetString(Arg.Any<string>());
    }
}

