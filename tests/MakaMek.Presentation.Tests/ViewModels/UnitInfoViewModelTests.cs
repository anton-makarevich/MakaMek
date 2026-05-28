using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class UnitInfoViewModelTests
{
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();

    [Fact]
    public void Constructor_SetsUnitFromFactory()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory);

        sut.Unit.ShouldBe(unit);
        sut.HasPilot.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_SetsHasPilotTrue_WhenPilotDataProvided()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var pilotData = new PilotData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, pilotData, _mechFactory);

        sut.HasPilot.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_AssignsPilotToUnit_WhenPilotDataProvided()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var pilotData = new PilotData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, pilotData, _mechFactory);

        unit.Pilot.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetResultAsync_ReturnsTaskThatCompletes_WhenCloseCommandExecuted()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory);

        var resultTask = sut.GetResultAsync();
        resultTask.IsCompleted.ShouldBeFalse();

        sut.CloseCommand.Execute(null);

        var result = await resultTask;
        result.ShouldBeNull();
    }

    private static Unit CreateRealUnit(UnitData unitData)
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        return factory.Create(unitData);
    }
}
