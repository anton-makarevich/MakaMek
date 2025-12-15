using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class StartPhaseCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();

    private StartPhaseCommand CreateCommand()
    {
        return new StartPhaseCommand
        {
            GameOriginId = _gameId,
            Phase = PhaseNames.Movement,
            Timestamp = DateTime.UtcNow
        };
    }


    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _localizationService.GetString("Command_StartPhase").Returns("formatted phase command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted phase command");
        _localizationService.Received(1).GetString("Command_StartPhase");
    }
}