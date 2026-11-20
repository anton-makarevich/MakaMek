using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Publishes the local player's ready command. Exposed as an extension on the client game so the
/// setup session does not depend on the command payload.
/// </summary>
public static class ClientGameSetupExtensions
{
    extension(ClientGame game)
    {
        public void PublishSetReady(PlayerViewModel playerVm)
        {
            var readyCommand = new UpdatePlayerStatusCommand
            {
                GameOriginId = game.Id,
                PlayerId = playerVm.Player.Id,
                PlayerStatus = PlayerStatus.Ready
            };
            game.SetPlayerReady(readyCommand);
        }
    }
}
