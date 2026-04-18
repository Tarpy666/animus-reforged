using System.Text;
using AnimusReforged.Logging;
using AnimusReforged.Utilities;

namespace AnimusReforged.Mods.Core;

/// <summary>
/// Manages uMod configuration and setup for games.
/// </summary>
public class UModManager
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly UnicodeEncoding UnicodeNoBom = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);

    /// <summary>
    /// Sets up the uMod AppData directory and config file with the specified game paths.
    /// </summary>
    /// <param name="gamePaths">The paths to the game executables or directories.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <exception cref="ArgumentException">Thrown when gamePaths is null or empty.</exception>
    public static async Task SetupAppdata(IReadOnlyList<string> gamePaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gamePaths);
        if (gamePaths.Count == 0)
        {
            // Shouldn't ever happen
            throw new ArgumentException("Game paths cannot be empty.", nameof(gamePaths));
        }

        Logger.Debug<UModManager>("Setting up uMod AppData");
        Directory.CreateDirectory(FilePaths.UModAppdata);

        foreach (string gamePath in gamePaths)
        {
            Logger.Debug<UModManager>($"Game path: {gamePath}");

            if (File.Exists(FilePaths.UModConfig))
            {
                await AppendGamePathIfMissing(gamePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Logger.Debug<UModManager>("Creating new uMod AppData config file");
                await File.WriteAllTextAsync(FilePaths.UModConfig, gamePath, UnicodeNoBom, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Appends the game executable path to uMod's AppData config file
    /// </summary>
    /// <param name="gamePath">Path to the game.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private static async Task AppendGamePathIfMissing(string gamePath, CancellationToken cancellationToken)
    {
        Logger.Debug<UModManager>("Checking existing config file");

        string[] lines = await File.ReadAllLinesAsync(FilePaths.UModConfig, UnicodeNoBom, cancellationToken).ConfigureAwait(false);

        HashSet<string> existingPaths = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);

        if (existingPaths.Contains(gamePath))
        {
            Logger.Debug<UModManager>("Path already exists in config file");
        }
        else
        {
            Logger.Debug<UModManager>("Appending path to config file");

            string fileContent = await File.ReadAllTextAsync(FilePaths.UModConfig, UnicodeNoBom, cancellationToken).ConfigureAwait(false);
            bool endsWithNewline = fileContent.EndsWith(Environment.NewLine) || fileContent.EndsWith("\n") || fileContent.EndsWith("\r\n");

            string textToAppend = endsWithNewline ? gamePath : Environment.NewLine + gamePath;
            await File.AppendAllTextAsync(FilePaths.UModConfig, textToAppend, UnicodeNoBom, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the specified game paths from the uMod config file.
    /// </summary>
    /// <param name="gamePaths">The paths to remove from the config file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <exception cref="ArgumentException">Thrown when gamePaths is null or empty.</exception>
    public static async Task RemoveGameFromAppdata(IReadOnlyList<string> gamePaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gamePaths);
        if (gamePaths.Count == 0)
        {
            throw new ArgumentException("Game paths cannot be empty.", nameof(gamePaths));
        }

        if (!File.Exists(FilePaths.UModConfig))
        {
            Logger.Error<UModManager>("uMod config file not found");
            return;
        }

        string[] lines = await File.ReadAllLinesAsync(FilePaths.UModConfig, UnicodeNoBom, cancellationToken).ConfigureAwait(false);

        // Filter out any lines matching any of the provided paths
        string[] updatedLines = lines
            .Where(line => !gamePaths.Any(path => line.Trim().Equals(path, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (updatedLines.Length < lines.Length)
        {
            await File.WriteAllLinesAsync(FilePaths.UModConfig, updatedLines, UnicodeNoBom, cancellationToken).ConfigureAwait(false);
            Logger.Debug<UModManager>($"Removed {lines.Length - updatedLines.Length} path(s) from uMod config");
        }
        else
        {
            Logger.Warning<UModManager>("None of the provided paths were found in config file");
        }
    }

    /// <summary>
    /// Sets up the uMod save file and template for the specified game.
    /// </summary>
    /// <param name="gamePaths">The paths to the game executables or directories.</param>
    /// <param name="templateName">The name of the template file to create.</param>
    /// <param name="modFilePaths">Collection of mod file paths to include in the template as enabled mods.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <exception cref="ArgumentException">Thrown when gamePaths is null/empty or templateName is null or empty.</exception>
    public static async Task SetupSaveFile(IReadOnlyList<string> gamePaths, string templateName, IEnumerable<string>? modFilePaths = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gamePaths);
        if (gamePaths.Count == 0)
        {
            // This shouldn't happen
            throw new ArgumentException("Game paths cannot be empty.", nameof(gamePaths));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        Directory.CreateDirectory(FilePaths.UModTemplates);

        // Ensure status file exists
        if (!File.Exists(FilePaths.UModStatusFile))
        {
            Logger.Debug<UModManager>("Creating uMod status file");
            await File.WriteAllTextAsync(FilePaths.UModStatusFile, "Enabled=1", Encoding.ASCII, cancellationToken).ConfigureAwait(false);
        }

        Logger.Debug<UModManager>("Setting up uMod template");
        string templatePath = Path.Combine(FilePaths.UModTemplates, templateName);

        // Build template content (only needs to be done once)
        string content = BuildTemplateContent(modFilePaths);
        await File.WriteAllTextAsync(templatePath, content, Utf8NoBom, cancellationToken).ConfigureAwait(false);

        // Append all game paths to save files
        IEnumerable<string> saveFileEntries = gamePaths.Select(gamePath => $"{gamePath}|{templatePath}\n");
        string saveFileContent = string.Concat(saveFileEntries);
        Logger.Debug<UModManager>($"Save file entries:\n{saveFileContent}");
        await File.AppendAllTextAsync(FilePaths.UModSaveFiles, saveFileContent, UnicodeNoBom, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates `template.txt` file used to load all uMod mods
    /// </summary>
    /// <param name="modFilePaths">Paths to the mod files.</param>
    /// <returns>template.txt content as a string.</returns>
    private static string BuildTemplateContent(IEnumerable<string>? modFilePaths = null)
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append("SaveAllTextures:0\n");
        sb.Append("SaveSingleTexture:0\n");
        sb.Append("FontColour:255,0,0\n");
        sb.Append("TextureColour:0,255,0\n");

        if (modFilePaths?.Any() == true)
        {
            foreach (string modPath in modFilePaths.Where(path => !string.IsNullOrEmpty(path)))
            {
                sb.Append($"Add_true:{modPath}\n");
            }
        }

        return sb.ToString();
    }
}