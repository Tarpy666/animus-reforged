using System.Diagnostics;
using AnimusReforged.Logging;
using AnimusReforged.Models.Launcher;
using AnimusReforged.Utilities;

namespace AnimusReforged.Launcher;

/// <summary>
/// Provides helper methods for launching external processes such as uMod and the game executable.
/// </summary>
public class Helper
{
    /// <summary>
    /// Launches the uMod process with a minimized window style.
    /// </summary>
    /// <returns>The Process object representing the launched uMod instance.</returns>
    public static Process LaunchuMod()
    {
        try
        {
            Process uMod = new Process();
            uMod.StartInfo.FileName = FilePaths.UModExecutable;
            uMod.StartInfo.WorkingDirectory = FilePaths.UModLocation;
            uMod.StartInfo.UseShellExecute = true;
            uMod.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

            bool started = uMod.Start();
            if (!started)
            {
                Logger.Error<Helper>($"Failed to start uMod process: {FilePaths.UModExecutable}");
                throw new InvalidOperationException($"Could not start uMod process: {FilePaths.UModExecutable}");
            }

            return uMod;
        }
        catch (Exception ex)
        {
            Logger.Error<Helper>($"Error launching uMod: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Launches a game executable with the application's base directory as the working directory.
    /// </summary>
    /// <param name="executablePath">The path to the game executable to launch.</param>
    /// <returns>The Process object representing the launched game instance.</returns>
    public static Process LaunchGame(string executablePath)
    {
        try
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                Logger.Error<Helper>("Game executable path is null or empty");
                throw new ArgumentException("Game executable path cannot be null or empty", nameof(executablePath));
            }

            if (!File.Exists(executablePath))
            {
                Logger.Error<Helper>($"Game executable does not exist: {executablePath}");
                throw new FileNotFoundException($"Game executable not found: {executablePath}");
            }

            Process game = new Process();
            game.StartInfo.FileName = executablePath;
            game.StartInfo.WorkingDirectory = AbsolutePath.BaseDirectory();
            game.StartInfo.UseShellExecute = true;

            bool started = game.Start();
            if (!started)
            {
                Logger.Error<Helper>($"Failed to start game process: {executablePath}");
                throw new InvalidOperationException($"Could not start game process: {executablePath}");
            }

            return game;
        }
        catch (Exception ex)
        {
            Logger.Error<Helper>($"Error launching game: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Waits synchronously until all known game-related processes have exited.
    /// Uses a state machine to handle DRM restarts cleanly:
    /// - Only checks for DRM respawn once, on the first short-lived session
    /// - Uses short timeouts to minimize delay on quick legitimate closes
    /// - After DRM is confirmed, the next exit is treated as final immediately
    /// </summary>
    /// <param name="executableNames">The list of executable names to monitor for exit.</param>
    /// <param name="options">Configuration options for monitoring behavior. Uses defaults if null.</param>
    public static void WaitForGameExit(IEnumerable<string> executableNames, GameMonitorOptions? options = null)
    {
        options ??= new GameMonitorOptions();
        string[] names = StripExtensions(executableNames);

        MonitorState state = MonitorState.WaitingForInitialLaunch;
        Stopwatch sessionTimer = new Stopwatch();
        Stopwatch waitTimer = new Stopwatch();

        Logger.Info<Helper>($"Monitoring game processes: {string.Join(", ", names)}");
        Logger.Info<Helper>($"Options: polling={options.PollingInterval.TotalMilliseconds}ms, " +
                            $"initialTimeout={options.InitialLaunchTimeout.TotalSeconds}s, " +
                            $"drmThreshold={options.DrmThreshold.TotalSeconds}s, " +
                            $"drmRespawnTimeout={options.DrmRespawnTimeout.TotalSeconds}s");

        waitTimer.Start();

        while (state != MonitorState.Exited)
        {
            bool isRunning = AreAnyProcessesRunning(names);

            state = ProcessStateTransition(state, isRunning,
                sessionTimer, waitTimer,
                options);

            if (state != MonitorState.Exited)
            {
                Thread.Sleep(options.PollingInterval);
            }
        }

        Logger.Info<Helper>("All monitored game processes have exited");
    }

    /// <summary>
    /// Waits asynchronously until all known game-related processes have exited.
    /// Uses a state machine to handle DRM restarts cleanly:
    /// - Only checks for DRM respawn once, on the first short-lived session
    /// - Uses short timeouts to minimize delay on quick legitimate closes
    /// - After DRM is confirmed, the next exit is treated as final immediately
    /// </summary>
    /// <param name="executableNames">The list of executable names to monitor for exit.</param>
    /// <param name="options">Configuration options for monitoring behavior. Uses defaults if null.</param>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    public static async Task WaitForGameExitAsync(IEnumerable<string> executableNames,
        GameMonitorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GameMonitorOptions();
        string[] names = StripExtensions(executableNames);

        MonitorState state = MonitorState.WaitingForInitialLaunch;
        Stopwatch sessionTimer = new Stopwatch();
        Stopwatch waitTimer = new Stopwatch();

        Logger.Info<Helper>($"Monitoring game processes: {string.Join(", ", names)}");
        Logger.Info<Helper>($"Options: polling={options.PollingInterval.TotalMilliseconds}ms, " +
                            $"initialTimeout={options.InitialLaunchTimeout.TotalSeconds}s, " +
                            $"drmThreshold={options.DrmThreshold.TotalSeconds}s, " +
                            $"drmRespawnTimeout={options.DrmRespawnTimeout.TotalSeconds}s");

        waitTimer.Start();

        while (state != MonitorState.Exited)
        {
            bool isRunning = AreAnyProcessesRunning(names);

            state = ProcessStateTransition(state, isRunning,
                sessionTimer, waitTimer,
                options);

            if (state != MonitorState.Exited)
            {
                await Task.Delay(options.PollingInterval, cancellationToken);
            }
        }

        Logger.Info<Helper>("All monitored game processes have exited");
    }

    /// <summary>
    /// Processes a single state transition in the game monitoring state machine.
    /// </summary>
    /// <param name="currentState">The current state of the monitor.</param>
    /// <param name="isRunning">Whether any monitored game process is currently running.</param>
    /// <param name="sessionTimer">Stopwatch tracking how long the current game session has been running.</param>
    /// <param name="waitTimer">Stopwatch tracking how long we've been waiting in timeout states.</param>
    /// <param name="options">Configuration options for monitoring behavior.</param>
    /// <returns>The new state after processing the transition.</returns>
    private static MonitorState ProcessStateTransition(MonitorState currentState, bool isRunning,
        Stopwatch sessionTimer, Stopwatch waitTimer,
        GameMonitorOptions options)
    {
        switch (currentState)
        {
            case MonitorState.WaitingForInitialLaunch:
                if (isRunning)
                {
                    Logger.Info<Helper>("Game process detected");
                    sessionTimer.Restart();
                    return MonitorState.GameRunning;
                }

                if (waitTimer.Elapsed >= options.InitialLaunchTimeout)
                {
                    Logger.Warning<Helper>("Game process never appeared within initial timeout");
                    return MonitorState.Exited;
                }

                return MonitorState.WaitingForInitialLaunch;

            case MonitorState.GameRunning:
                if (isRunning)
                {
                    return MonitorState.GameRunning;
                }

                sessionTimer.Stop();
                TimeSpan sessionDuration = sessionTimer.Elapsed;
                Logger.Info<Helper>($"Game exited after {sessionDuration.TotalSeconds:F1}s");

                // Long session — definitely a real exit
                if (sessionDuration > options.DrmThreshold)
                {
                    Logger.Info<Helper>("Session exceeded DRM threshold — treating as final exit");
                    return MonitorState.Exited;
                }

                // Short session — might be DRM, wait briefly for respawn
                Logger.Info<Helper>($"Short session ({sessionDuration.TotalSeconds:F1}s) — " +
                                    $"waiting up to {options.DrmRespawnTimeout.TotalSeconds}s for possible DRM respawn...");
                waitTimer.Restart();
                return MonitorState.WaitingForDrmRespawn;

            case MonitorState.WaitingForDrmRespawn:
                if (isRunning)
                {
                    Logger.Info<Helper>("Game respawned — DRM restart confirmed, now monitoring real session");
                    sessionTimer.Restart();
                    return MonitorState.MonitoringAfterDrmRestart;
                }

                if (waitTimer.Elapsed >= options.DrmRespawnTimeout)
                {
                    Logger.Info<Helper>("No respawn detected within timeout — treating as final exit");
                    return MonitorState.Exited;
                }

                return MonitorState.WaitingForDrmRespawn;

            case MonitorState.MonitoringAfterDrmRestart:
                if (isRunning)
                {
                    return MonitorState.MonitoringAfterDrmRestart;
                }

                sessionTimer.Stop();
                Logger.Info<Helper>($"Game exited after {sessionTimer.Elapsed.TotalSeconds:F1}s (post-DRM session) — treating as final exit");
                return MonitorState.Exited;

            case MonitorState.Exited:
                return MonitorState.Exited;

            default:
                Logger.Error<Helper>($"Unknown state: {currentState}");
                return MonitorState.Exited;
        }
    }

    /// <summary>
    /// Checks whether any process from the provided list of process names is currently running.
    /// </summary>
    /// <param name="processNames">The process names to check (without .exe extension).</param>
    /// <returns>True if at least one process from the list is running, otherwise false.</returns>
    private static bool AreAnyProcessesRunning(string[] processNames)
    {
        foreach (string name in processNames)
        {
            Process[] processes = Process.GetProcessesByName(name);
            if (processes.Length <= 0)
            {
                continue;
            }

            foreach (Process process in processes)
            {
                process.Dispose();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Strips the .exe extension from executable names since
    /// <see cref="Process.GetProcessesByName"/> does not accept extensions.
    /// </summary>
    /// <param name="executableNames">The executable names to strip.</param>
    /// <returns>An array of process names without the .exe extension.</returns>
    private static string[] StripExtensions(IEnumerable<string> executableNames)
    {
        return executableNames
            .Select(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name[..^4]
                : name)
            .ToArray();
    }
}