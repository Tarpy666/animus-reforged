namespace AnimusReforged.Models.Launcher;

/// <summary>
/// Configuration options for game exit monitoring.
/// </summary>
public class GameMonitorOptions
{
    /// <summary>
    /// How often to poll for process status changes.
    /// Default: 500ms
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum time to wait for the game to appear on initial launch.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan InitialLaunchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum session duration to consider as a potential DRM restart.
    /// Sessions shorter than this may trigger the DRM respawn wait.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan DrmThreshold { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to wait for the game to respawn after a short session.
    /// Only applies once on the first short exit.
    /// Default: 8 seconds
    /// </summary>
    public TimeSpan DrmRespawnTimeout { get; set; } = TimeSpan.FromSeconds(30);
}