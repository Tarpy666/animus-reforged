using System.Diagnostics;
using AnimusReforged.Logging;
using AnimusReforged.Utilities;

namespace AnimusReforged.Launcher;

/// <summary>
/// Provides helper methods for launching external processes such as uMod and the game executable.
/// </summary>
public class Helper
{
    /// <summary>
    /// Launches the uMod process with minimized window style.
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
    /// Distinguishes between DRM restarts and real exits by measuring how long the
    /// game ran before exiting. A DRM restart typically exits almost immediately,
    /// whereas a real game session runs for a meaningful amount of time.
    /// Once a DRM restart is confirmed, subsequent exits are treated as final immediately.
    /// </summary>
    /// <param name="executableNames">The list of executable names to monitor for exit.</param>
    /// <param name="pollingInterval">How often to check for running processes (defaults to 2 seconds).</param>
    /// <param name="initialTimeout">How long to wait for the game to appear initially (defaults to 30 seconds).</param>
    /// <param name="drmThreshold">Maximum runtime to consider an exit as a DRM restart (defaults to 30 seconds).</param>
    public static void WaitForGameExit(IEnumerable<string> executableNames,
        TimeSpan? pollingInterval = null, TimeSpan? initialTimeout = null, TimeSpan? drmThreshold = null)
    {
        TimeSpan interval = pollingInterval ?? TimeSpan.FromSeconds(2);
        TimeSpan timeout = initialTimeout ?? TimeSpan.FromSeconds(30);
        TimeSpan threshold = drmThreshold ?? TimeSpan.FromSeconds(30);
        string[] names = StripExtensions(executableNames);
        bool drmRestartConfirmed = false;

        Logger.Info<Helper>($"Monitoring game processes: {string.Join(", ", names)}");

        // Wait for the game to appear initially
        if (!WaitForProcessToAppear(names, interval, timeout))
        {
            Logger.Warning<Helper>("Game process never appeared within initial timeout");
            return;
        }

        Logger.Info<Helper>("Game process detected");

        while (true)
        {
            // Track how long the game runs this cycle
            Stopwatch sessionTimer = Stopwatch.StartNew();

            // Wait while the game is running
            while (AreAnyProcessesRunning(names))
            {
                Thread.Sleep(interval);
            }

            sessionTimer.Stop();
            TimeSpan sessionDuration = sessionTimer.Elapsed;

            Logger.Info<Helper>($"All game processes exited after {sessionDuration.TotalSeconds:F1}s");

            // If DRM restart was already confirmed, this is the real game exiting — done
            if (drmRestartConfirmed)
            {
                Logger.Info<Helper>("Game exited after DRM restart — treating as final exit");
                break;
            }

            // If the game ran longer than the threshold, it was a real session — done
            if (sessionDuration > threshold)
            {
                Logger.Info<Helper>("Session exceeded DRM threshold — treating as final exit");
                break;
            }

            // Short session — likely a DRM restart, wait for respawn
            Logger.Info<Helper>("Short session detected (possible DRM restart) — waiting for game to respawn...");

            if (WaitForProcessToAppear(names, interval, timeout))
            {
                Logger.Info<Helper>("Game respawned (DRM restart confirmed) — subsequent exit will be treated as final");
                drmRestartConfirmed = true;
                continue;
            }

            // Game did not respawn despite short session — treat as final exit
            Logger.Info<Helper>("Game did not respawn within timeout — treating as final exit");
            break;
        }

        Logger.Info<Helper>("All monitored game processes have exited");
    }

    /// <summary>
    /// Waits asynchronously until all known game-related processes have exited.
    /// Distinguishes between DRM restarts and real exits by measuring how long the
    /// game ran before exiting. A DRM restart typically exits almost immediately,
    /// whereas a real game session runs for a meaningful amount of time.
    /// Once a DRM restart is confirmed, subsequent exits are treated as final immediately.
    /// </summary>
    /// <param name="executableNames">The list of executable names to monitor for exit.</param>
    /// <param name="pollingInterval">How often to check for running processes (defaults to 2 seconds).</param>
    /// <param name="initialTimeout">How long to wait for the game to appear initially (defaults to 30 seconds).</param>
    /// <param name="drmThreshold">Maximum runtime to consider an exit as a DRM restart (defaults to 30 seconds).</param>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    public static async Task WaitForGameExitAsync(IEnumerable<string> executableNames,
        TimeSpan? pollingInterval = null, TimeSpan? initialTimeout = null, TimeSpan? drmThreshold = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan interval = pollingInterval ?? TimeSpan.FromSeconds(2);
        TimeSpan timeout = initialTimeout ?? TimeSpan.FromSeconds(30);
        TimeSpan threshold = drmThreshold ?? TimeSpan.FromSeconds(30);
        string[] names = StripExtensions(executableNames);
        bool drmRestartConfirmed = false;

        Logger.Info<Helper>($"Monitoring game processes: {string.Join(", ", names)}");

        // Wait for the game to appear initially
        if (!await WaitForProcessToAppearAsync(names, interval, timeout, cancellationToken))
        {
            Logger.Warning<Helper>("Game process never appeared within initial timeout");
            return;
        }

        Logger.Info<Helper>("Game process detected");

        while (true)
        {
            // Track how long the game runs this cycle
            Stopwatch sessionTimer = Stopwatch.StartNew();

            // Wait while the game is running
            while (AreAnyProcessesRunning(names))
            {
                await Task.Delay(interval, cancellationToken);
            }

            sessionTimer.Stop();
            TimeSpan sessionDuration = sessionTimer.Elapsed;

            Logger.Info<Helper>($"All game processes exited after {sessionDuration.TotalSeconds:F1}s");

            // If DRM restart was already confirmed, this is the real game exiting — done
            if (drmRestartConfirmed)
            {
                Logger.Info<Helper>("Game exited after DRM restart — treating as final exit");
                break;
            }

            // If the game ran longer than the threshold, it was a real session — done
            if (sessionDuration > threshold)
            {
                Logger.Info<Helper>("Session exceeded DRM threshold — treating as final exit");
                break;
            }

            // Short session — likely a DRM restart, wait for respawn
            Logger.Info<Helper>("Short session detected (possible DRM restart) — waiting for game to respawn...");

            if (await WaitForProcessToAppearAsync(names, interval, timeout, cancellationToken))
            {
                Logger.Info<Helper>("Game respawned (DRM restart confirmed) — subsequent exit will be treated as final");
                drmRestartConfirmed = true;
                continue;
            }

            // Game did not respawn despite short session — treat as final exit
            Logger.Info<Helper>("Game did not respawn within timeout — treating as final exit");
            break;
        }

        Logger.Info<Helper>("All monitored game processes have exited");
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
    /// Waits synchronously for any of the specified game processes to appear within the given timeout.
    /// Used to detect game spawns after DRM restarts or initial launch delays.
    /// </summary>
    /// <param name="processNames">The process names to watch for (without .exe extension).</param>
    /// <param name="pollingInterval">How often to check for the process.</param>
    /// <param name="timeout">Maximum time to wait for the process to appear.</param>
    /// <returns>True if a process appeared within the timeout, false otherwise.</returns>
    private static bool WaitForProcessToAppear(string[] processNames,
        TimeSpan pollingInterval, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (AreAnyProcessesRunning(processNames))
            {
                return true;
            }

            Thread.Sleep(pollingInterval);
        }

        return false;
    }

    /// <summary>
    /// Waits asynchronously for any of the specified game processes to appear within the given timeout.
    /// Used to detect game spawns after DRM restarts or initial launch delays.
    /// </summary>
    /// <param name="processNames">The process names to watch for (without .exe extension).</param>
    /// <param name="pollingInterval">How often to check for the process.</param>
    /// <param name="timeout">Maximum time to wait for the process to appear.</param>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    /// <returns>True if a process appeared within the timeout, false otherwise.</returns>
    private static async Task<bool> WaitForProcessToAppearAsync(string[] processNames,
        TimeSpan pollingInterval, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (AreAnyProcessesRunning(processNames))
            {
                return true;
            }

            await Task.Delay(pollingInterval, cancellationToken);
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