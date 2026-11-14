using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Builds the relay publisher options from the hub configuration pinned for the room lifecycle,
/// so callers keep working with the options they already hold instead of the transport-specific
/// publisher type.
/// </summary>
internal static class RelayClientOptionsExtensions
{
    extension(RelayClientOptions options)
    {
        /// <summary>
        /// The publisher options for joining or hosting the room with the given code and ticket,
        /// targeting the hub the options were selected for.
        /// </summary>
        public RelayPublisherOptions CreatePublisherOptions(string roomCode, string relayTicket) =>
            new()
            {
                HubUrl = RelayHubDefaults.BuildHubUrl(options.BaseUrl),
                RoomCode = roomCode,
                RelayTicket = relayTicket
            };
    }
}
