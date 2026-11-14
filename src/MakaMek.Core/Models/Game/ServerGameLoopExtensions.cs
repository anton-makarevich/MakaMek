namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// The server's idle host loop. Kept out of <see cref="ServerGame"/> so the game class does not
/// reference the scheduling types; the call site (<c>serverGame.Start()</c>) is unchanged.
/// </summary>
public static class ServerGameLoopExtensions
{
    public static async Task Start(this ServerGame game)
    {
        // The game loop is driven by commands and phase transitions.
        // This method mainly keeps the server alive until disposed.
        while (!game.IsDisposed && !game.IsGameOver)
        {
            await Task.Delay(100); // Keep the task alive but idle
        }
    }
}
