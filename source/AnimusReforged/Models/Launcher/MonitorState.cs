namespace AnimusReforged.Models.Launcher;

/// <summary>
/// Represents the different states of the game monitoring process.
/// </summary>
public enum MonitorState
{
    /// <summary>
    /// Waiting for the game process to appear for the first time.
    /// </summary>
    WaitingForInitialLaunch,

    /// <summary>
    /// The game is currently running and being monitored.
    /// </summary>
    GameRunning,

    /// <summary>
    /// The game exited quickly — waiting to see if it respawns (possible DRM restart).
    /// </summary>
    WaitingForDrmRespawn,

    /// <summary>
    /// DRM restart was confirmed — now monitoring the real game session.
    /// </summary>
    MonitoringAfterDrmRestart,

    /// <summary>
    /// The game has fully exited.
    /// </summary>
    Exited
}