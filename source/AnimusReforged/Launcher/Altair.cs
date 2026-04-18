using System.Diagnostics;
using AnimusReforged.Logging;
using AnimusReforged.Models.Altair;
using AnimusReforged.Utilities;

namespace AnimusReforged.Launcher;

/// <summary>
/// Provides functionality for launching the Assassin's Creed game with optional uMod support.
/// Handles both synchronous and asynchronous launch operations with proper process management.
/// </summary>
public class Altair
{
    /// <summary>
    /// The known executable names associated with the Assassin's Creed (Altair) game.
    /// These are monitored to determine when the game has fully exited.
    /// </summary>
    private static readonly string[] GameExecutables =
    [
        "AssassinsCreed_Dx9.exe",
        "AssassinsCreed_Dx10.exe",
        "AssassinsCreed_Game.exe"
    ];

    /// <summary>
    /// Launches the Assassin's Creed game asynchronously with optional uMod support.
    /// Dynamically handles DRM restarts and waits for the game to fully exit before
    /// closing uMod if enabled.
    /// </summary>
    /// <param name="uModEnabled">Whether to launch uMod alongside the game (defaults to false).</param>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    public static async Task LaunchAsync(bool uModEnabled = false, CancellationToken cancellationToken = default)
    {
        Process? uMod = null;
        Process? game = null;

        try
        {
            if (uModEnabled)
            {
                Logger.Info<Altair>("Launching uMod");
                uMod = Helper.LaunchuMod();
            }

            Logger.Info<Altair>("Launching the game");
            game = Helper.LaunchGame(FilePaths.AltairExecutables[ExecutableType.DX9]);

            Logger.Info<Altair>("Waiting for the game to exit");
            await Helper.WaitForGameExitAsync(GameExecutables, cancellationToken: cancellationToken);
            Logger.Info<Altair>("Game exited");
        }
        finally
        {
            // Close uMod after game exits
            if (uModEnabled && uMod != null && !uMod.HasExited)
            {
                Logger.Info<Altair>("Closing uMod");
                uMod.CloseMainWindow();
                uMod.Dispose();
            }

            // Dispose of game process
            game?.Dispose();
        }
    }

    /// <summary>
    /// Launches the Assassin's Creed game synchronously with optional uMod support.
    /// Dynamically handles DRM restarts and blocks the calling thread until the game
    /// has fully exited before closing uMod if enabled.
    /// </summary>
    /// <param name="uModEnabled">Whether to launch uMod alongside the game (defaults to false).</param>
    public static void Launch(bool uModEnabled = false)
    {
        Process? uMod = null;
        Process? game = null;

        try
        {
            if (uModEnabled)
            {
                Logger.Info<Altair>("Launching uMod");
                uMod = Helper.LaunchuMod();
            }

            Logger.Info<Altair>("Launching the game");
            game = Helper.LaunchGame(FilePaths.AltairExecutables[ExecutableType.DX9]);

            Logger.Info<Altair>("Waiting for the game to exit");
            Helper.WaitForGameExit(GameExecutables);
            Logger.Info<Altair>("Game exited");
        }
        finally
        {
            // Close uMod after game exits
            if (uModEnabled && uMod != null && !uMod.HasExited)
            {
                Logger.Info<Altair>("Closing uMod");
                uMod.CloseMainWindow();
                uMod.Dispose();
            }

            // Dispose of game process
            game?.Dispose();
        }
    }
}