using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PlayerViewModelTests
{
    [Fact]
    public async Task ShowUnitInfoCommand_ShouldInvokeShowUnitInfoDelegate_WhenUnitExists()
    {
        var showUnitInfo = Substitute.For<Func<UnitData, PilotData?, Task>>();
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true, showUnitInfo: showUnitInfo);
        var unitData = MechFactoryTests.CreateDummyMechData();
        await sut.AddUnit(unitData);
        var unitId = sut.Units.First().Id!.Value;

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(unitId);

        await showUnitInfo.Received(1).Invoke(Arg.Is<UnitData>(u => u.Id == unitId), Arg.Any<PilotData?>());
    }

    [Fact]
    public async Task ShowUnitInfoCommand_ShouldNotInvokeDelegate_WhenUnitIdIsEmpty()
    {
        var showUnitInfo = Substitute.For<Func<UnitData, PilotData?, Task>>();
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true, showUnitInfo: showUnitInfo);

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(Guid.Empty);

        await showUnitInfo.DidNotReceive().Invoke(Arg.Any<UnitData>(), Arg.Any<PilotData?>());
    }

    [Fact]
    public async Task ShowUnitInfoCommand_ShouldNotInvokeDelegate_WhenUnitNotFound()
    {
        var showUnitInfo = Substitute.For<Func<UnitData, PilotData?, Task>>();
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true, showUnitInfo: showUnitInfo);
        var unknownId = Guid.NewGuid();

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(unknownId);

        await showUnitInfo.DidNotReceive().Invoke(Arg.Any<UnitData>(), Arg.Any<PilotData?>());
    }

    [Fact]
    public async Task ShowUnitInfoCommand_ShouldNotThrow_WhenDelegateIsNull()
    {
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true);
        var unitData = MechFactoryTests.CreateDummyMechData();
        await sut.AddUnit(unitData);
        var unitId = sut.Units.First().Id!.Value;

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(unitId);
    }

    [Fact]
    public async Task ShowUnitInfoCommand_ShouldPassCorrectPilotData_WhenUnitHasPilot()
    {
        var showUnitInfo = Substitute.For<Func<UnitData, PilotData?, Task>>();
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true, showUnitInfo: showUnitInfo);
        var unitData = MechFactoryTests.CreateDummyMechData();
        var pilotData = new PilotData();
        await sut.AddUnit(unitData, pilotData);
        var unitId = sut.Units.First().Id!.Value;

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(unitId);

        await showUnitInfo.Received(1).Invoke(
            Arg.Is<UnitData>(u => u.Id == unitId),
            Arg.Is<PilotData>(p => p.Equals(pilotData)));
    }

    [Fact]
    public async Task ShowUnitInfoCommand_ShouldPassDefaultPilotData_WhenUnitHasNoExplicitPilot()
    {
        var showUnitInfo = Substitute.For<Func<UnitData, PilotData?, Task>>();
        var player = new Player(PlayerData.CreateDefault(), PlayerControlType.Human);
        var sut = new PlayerViewModel(player, true, showUnitInfo: showUnitInfo);
        var unitData = MechFactoryTests.CreateDummyMechData();
        await sut.AddUnit(unitData);
        var unitId = sut.Units.First().Id!.Value;

        await ((IAsyncCommand<Guid>)sut.ShowUnitInfoCommand).ExecuteAsync(unitId);

        await showUnitInfo.Received(1).Invoke(
            Arg.Is<UnitData>(u => u.Id == unitId),
            Arg.Any<PilotData?>());
    }
}
