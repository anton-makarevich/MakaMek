using Shouldly;
using NSubstitute;
using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Models.Game.Commands.Server;
using Sanet.MekForge.Core.Models.Game.Phases;
using Sanet.MekForge.Core.Services.Localization;

namespace Sanet.MekForge.Core.Tests.Models.Game.Commands.Server;

public class ChangePhaseCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();

    private ChangePhaseCommand CreateCommand()
    {
        return new ChangePhaseCommand
        {
            GameOriginId = _gameId,
            Phase = PhaseNames.Movement
        };
    }


    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _localizationService.GetString("Command_ChangePhase").Returns("formatted phase command");

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted phase command");
        _localizationService.Received(1).GetString("Command_ChangePhase");
    }
}