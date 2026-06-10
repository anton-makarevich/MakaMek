using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement.Actions;

public class DisplaceUnitActionTests : GamePhaseTestsBase
{
    private readonly DisplaceUnitCommand _command;
    private readonly Guid _unitId = Guid.NewGuid();

    protected override void SetupSut()
    {
    }

    public DisplaceUnitActionTests()
    {
        _command = new DisplaceUnitCommand
        {
            UnitId = _unitId,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Process_WhenPublish_ShouldReturnCommand()
    {
        var sut = new DisplaceUnitAction(_command, publish: true);

        var result = sut.Process(Game);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ShouldBe(_command);
    }

    [Fact]
    public void Process_WhenNotPublish_ShouldReturnEmpty()
    {
        var sut = new DisplaceUnitAction(_command, publish: false);

        var result = sut.Process(Game);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_ShouldSetCommand()
    {
        var sut = new DisplaceUnitAction(_command);

        sut.Command.ShouldBe(_command);
    }

    [Fact]
    public void Process_ShouldReturnCommandWithCorrectData_WhenPublished()
    {
        var sut = new DisplaceUnitAction(_command, publish: true);

        var result = sut.Process(Game);

        var returnedCommand = (DisplaceUnitCommand)result[0];
        returnedCommand.UnitId.ShouldBe(_unitId);
        returnedCommand.FromCoordinates.ShouldBe(_command.FromCoordinates);
        returnedCommand.ToCoordinates.ShouldBe(_command.ToCoordinates);
        returnedCommand.NewFacing.ShouldBe((int)HexDirection.Top);
        returnedCommand.DisplacementReason.ShouldBe(DisplacementReason.DominoEffect);
    }
}
