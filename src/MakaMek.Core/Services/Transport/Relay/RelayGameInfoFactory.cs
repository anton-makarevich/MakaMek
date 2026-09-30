using Sanet.Transport.Relay.Contracts;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Single source for the game identity MakaMek reports to a relay hub. A hub serves rooms for
/// many games, so the host declares the title and version of the game a room belongs to at
/// creation and the hub echoes that identity back to every client that joins. Keeping the title
/// and version here means the hosting and joining flows cannot drift apart.
/// </summary>
public static class RelayGameInfoFactory
{
    /// <summary>
    /// The game title identifier MakaMek rooms carry. The hub accepts letters, digits, <c>'.'</c>,
    /// <c>'_'</c> and <c>'-'</c> only, and compares it with ordinal semantics, so a joiner can
    /// reject a room that belongs to a different game sharing the same hub.
    /// </summary>
    public const string GameTitle = "MakaMek";

    private static readonly Lazy<string> LazyGameVersion = new(ResolveGameVersion);

    /// <summary>
    /// The version MakaMek reports for its rooms, taken from this assembly's version in
    /// <c>Major.Minor.Build</c> form so it stays within the hub's length limit. It is
    /// informational only: clients are not required to match it, since every build has its own.
    /// </summary>
    public static string GameVersion => LazyGameVersion.Value;

    /// <summary>
    /// Builds the room game info for a hosted room.
    /// </summary>
    /// <param name="hostGameId">
    /// Id of the host's game instance. The hub requires a non-empty GUID and echoes it back to
    /// joiners, who use it to recognize commands that originate from the host.
    /// </param>
    public static RoomGameInfo Create(Guid hostGameId) =>
        new(hostGameId, GameTitle, GameVersion);

    /// <summary>
    /// Reads the assembly version without the revision component, which carries no information
    /// worth reporting. Falls back to a placeholder so a room is never created with a blank version.
    /// </summary>
    private static string ResolveGameVersion()
    {
        var version = typeof(RelayGameInfoFactory).Assembly.GetName().Version;
        return version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
