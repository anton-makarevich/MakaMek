using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class EndGameUnitViewModelTests
{
    private readonly MechFactory _mechFactory;

    public EndGameUnitViewModelTests()
    {
        _mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
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
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.Name.ShouldBe(mech.Name);
        sut.Chassis.ShouldBe(mech.Chassis);
        sut.Model.ShouldBe(mech.Model);
        sut.Tonnage.ShouldBe(mech.Tonnage);
        sut.WeightClass.ShouldBe(mech.Class.ToString());
        sut.Status.ShouldBe(mech.Status.ToString());
        sut.UnitData.Chassis.ShouldBe(mech.Chassis);
    }

    [Fact]
    public void IsDestroyed_ShouldBeTrue_WhenUnitIsDestroyed()
    {
        // Arrange
        var mech = CreateMech();
        // Destroy the mech by setting its status
        typeof(Unit).GetProperty("Status")!.SetValue(mech, UnitStatus.Destroyed);

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
        sut.IsAlive.ShouldBeFalse();
    }

    [Fact]
    public void IsAlive_ShouldBeTrue_WhenUnitIsNotDestroyed()
    {
        // Arrange
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsAlive.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void PilotName_ShouldReturnPilotName_WhenPilotExists()
    {
        // Arrange
        var mech = CreateMech();
        var pilot = new MechWarrior("Test", "Pilot");
        typeof(Unit).GetProperty("Pilot")!.SetValue(mech, pilot);

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.PilotName.ShouldBe("Test Pilot");
    }

    [Fact]
    public void PilotName_ShouldBeNull_WhenNoPilot()
    {
        // Arrange
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.PilotName.ShouldBeNull();
    }

    [Fact]
    public void IsPilotDead_ShouldBeTrue_WhenPilotIsDead()
    {
        // Arrange
        var mech = CreateMech();
        var pilot = new MechWarrior("Test", "Pilot");
        // Kill the pilot by applying enough hits
        for (var i = 0; i < 6; i++)
        {
            pilot.Hit();
        }
        typeof(Unit).GetProperty("Pilot")!.SetValue(mech, pilot);

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsPilotDead.ShouldBeTrue();
    }

    [Fact]
    public void IsPilotDead_ShouldBeFalse_WhenNoPilot()
    {
        // Arrange
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsPilotDead.ShouldBeFalse();
    }

    [Fact]
    public void IsPilotUnconscious_ShouldBeTrue_WhenPilotIsUnconscious()
    {
        // Arrange
        var mech = CreateMech();
        var pilot = new MechWarrior("Test", "Pilot");
        // Make pilot unconscious by applying hits and knocking unconscious
        for (int i = 0; i < 3; i++)
        {
            pilot.Hit();
        }
        pilot.KnockUnconscious(1);
        typeof(Unit).GetProperty("Pilot")!.SetValue(mech, pilot);

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsPilotUnconscious.ShouldBeTrue();
    }

    [Fact]
    public void IsPilotUnconscious_ShouldBeFalse_WhenPilotIsConscious()
    {
        // Arrange
        var mech = CreateMech();
        var pilot = new MechWarrior("Test", "Pilot");
        typeof(Unit).GetProperty("Pilot")!.SetValue(mech, pilot);

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsPilotUnconscious.ShouldBeFalse();
    }

    [Fact]
    public void IsPilotUnconscious_ShouldBeFalse_WhenNoPilot()
    {
        // Arrange
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.IsPilotUnconscious.ShouldBeFalse();
    }

    [Fact]
    public void WeightClass_ShouldReturnCorrectClass()
    {
        // Arrange
        var mech = CreateMech();

        // Act
        var sut = new EndGameUnitViewModel(mech);

        // Assert
        sut.WeightClass.ShouldBe(mech.Class.ToString());
    }
}

