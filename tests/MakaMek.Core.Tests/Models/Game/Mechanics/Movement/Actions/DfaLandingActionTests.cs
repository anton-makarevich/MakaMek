using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement.Actions;

public class DfaLandingActionTests : GamePhaseTestsBase
{
    private readonly DfaLandingCommand _command = new()
    {
        UnitId = Guid.NewGuid(),
        LandingKind = DfaLandingKind.AttackerLanding,
        GameOriginId = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow
    };

    protected override void SetupSut()
    {
    }

    [Fact]
    public void Process_WhenPublish_ShouldReturnCommand()
    {
        var sut = new DfaLandingAction(_command, publish: true);

        var result = sut.Process(Game);

        result.ShouldHaveSingleItem().ShouldBe(_command);
    }

    [Fact]
    public void Process_WhenNotPublish_ShouldReturnEmpty()
    {
        var sut = new DfaLandingAction(_command, publish: false);

        sut.Process(Game).ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_ShouldSetCommand()
    {
        var sut = new DfaLandingAction(_command);

        sut.Command.ShouldBe(_command);
    }
}
