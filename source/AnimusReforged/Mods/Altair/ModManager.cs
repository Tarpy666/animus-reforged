using System.Linq;
using AnimusReforged.Logging;
using AnimusReforged.Models;
using AnimusReforged.Models.Mods;
using AnimusReforged.Mods.Core;
using AnimusReforged.Settings;
using AnimusReforged.Utilities;

namespace AnimusReforged.Mods.Altair;

/// <summary>
/// Manages the downloading, installation, and setup of mods for the Altair game.
/// Handles various mod types including ASI loaders, patches, and overhauls.
/// </summary>
public class ModManager
{
    private static readonly DownloadManager DownloadManagerInstance = new DownloadManager();
    private static readonly string DownloadsDirectoryPath = FilePaths.DownloadsDirectory;
    private static readonly string ScriptsDirectoryPath = FilePaths.ScriptsDirectory;
    private static readonly string OverhaulDirectoryPath = FilePaths.OverhaulDirectory;

    /// <summary>
    /// Initializes the mod manager by loading the Altair manifest asynchronously.
    /// This should be called before performing any mod operations.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation</returns>
    public static async Task InitializeAsync()
    {
        await ManifestService.GetManifestAsync(ManifestType.Altair);
    }

    /// <summary>
    /// Downloads a mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier of the mod to download</param>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    /// <exception cref="Exception">Thrown when the download fails</exception>
    private static async Task DownloadModAsync(string modIdentifier, Action<int> progressCallback)
    {
        ModDefinition mod = ManifestService.GetMod(modIdentifier);
        Logger.Info<ModManager>($"Downloading {mod.Name}");

        DownloadManagerInstance.ProgressChanged += progressCallback;
        string savePath = Path.Combine(DownloadsDirectoryPath, Path.GetFileName(mod.Url));
        Logger.Debug<ModManager>($"Save path: {savePath}");

        try
        {
            await DownloadManagerInstance.DownloadFileAsync(mod.Url, savePath);
        }
        catch (Exception ex)
        {
            Logger.Error<ModManager>($"Failed to download {mod.Name}");
            Logger.LogExceptionDetails<ModManager>(ex);
            throw new Exception($"Failed to download {mod.Name}", ex);
        }
        finally
        {
            DownloadManagerInstance.ProgressChanged -= progressCallback;
        }

        Logger.Info<ModManager>("Download complete");
    }

    /// <summary>
    /// Extracts a downloaded mod archive to the specified output directory.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier of the mod to extract</param>
    /// <param name="outputDirectoryPath">The directory path where the mod should be extracted</param>
    /// <param name="fileFilters">Optional array of file extensions to filter during extraction (e.g., [".ini", ".asi"])</param>
    /// <exception cref="Exception">Thrown when the extraction fails</exception>
    private static void ExtractMod(string modIdentifier, string outputDirectoryPath, string[]? fileFilters = null)
    {
        ModDefinition mod = ManifestService.GetMod(modIdentifier);
        Logger.Info<ModManager>($"Extracting {mod.Name}");

        string archivePath = Path.Combine(DownloadsDirectoryPath, Path.GetFileName(mod.Url));
        Logger.Debug<ModManager>($"Archive location: {archivePath}");
        Logger.Debug<ModManager>($"Extraction path: {outputDirectoryPath}");

        try
        {
            ArchiveExtractor.ExtractArchive(archivePath, outputDirectoryPath, fileFilters);
        }
        catch (Exception ex)
        {
            Logger.Error<ModManager>($"Failed to extract {mod.Name}");
            Logger.LogExceptionDetails<ModManager>(ex);
            throw new Exception($"Failed to extract {mod.Name}", ex);
        }
    }

    #region ASI Loader Methods

    /// <summary>
    /// Downloads the ASI loader mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadAsiLoader(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.AsiLoader, progressCallback);

    /// <summary>
    /// Installs the ASI loader mod by extracting it to the base directory and creating the scripts directory.
    /// </summary>
    public static void InstallAsiLoader()
    {
        ExtractMod(ModIdentifiers.AsiLoader, AbsolutePath.BaseDirectory());
        Directory.CreateDirectory(ScriptsDirectoryPath);
    }

    /// <summary>
    /// Uninstalls the ASI loader mod by removing its files and cleaning up configurations.
    /// </summary>
    public static void UninstallAsiLoader()
    {
        Logger.Info<ModManager>("Uninstalling ASI Loader");

        // Find and remove ASI loader files from the base directory
        if (File.Exists(FilePaths.AsiLoader))
        {
            Logger.Debug<ModManager>($"ASI Loader File found ({FilePaths.AsiLoader})");
            Logger.Info<ModManager>("Deleting ASI Loader File");
            File.Delete(FilePaths.AsiLoader);
        }
        else
        {
            Logger.Warning<ModManager>("ASI Loader File not found (Could be already uninstalled)");
        }

        // Remove scripts directory if it exists
        if (Directory.Exists(ScriptsDirectoryPath))
        {
            try
            {
                Directory.Delete(ScriptsDirectoryPath, true);
                Logger.Debug<ModManager>($"Deleted scripts directory: {ScriptsDirectoryPath}");
            }
            catch (Exception ex)
            {
                Logger.Warning<ModManager>($"Could not delete scripts directory {ScriptsDirectoryPath}: {ex.Message}");
            }
        }

        Logger.Info<ModManager>("ASI loader uninstalled successfully");
    }

    #endregion

    #region Eagle Patch Methods

    /// <summary>
    /// Downloads the Eagle patch mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadEaglePatch(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.EaglePatch, progressCallback);

    /// <summary>
    /// Installs the Eagle patch mod by extracting specific file types to the scripts directory.
    /// </summary>
    public static void InstallEaglePatch() => ExtractMod(ModIdentifiers.EaglePatch, ScriptsDirectoryPath, [".ini", ".asi"]);

    #endregion

    #region Altair Fix Methods

    /// <summary>
    /// Downloads the Altair fix mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadAltairFix(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.AltairFix, progressCallback);

    /// <summary>
    /// Installs the Altair fix mod by extracting specific file types to the scripts directory.
    /// </summary>
    public static void InstallAltairFix() => ExtractMod(ModIdentifiers.AltairFix, ScriptsDirectoryPath, [".ini", ".asi"]);

    #endregion

    #region ReShade Methods

    /// <summary>
    /// Downloads the ReShade mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadReShade(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.ReShade, progressCallback);

    /// <summary>
    /// Installs the ReShade mod by extracting it to the scripts' directory.
    /// </summary>
    public static void InstallReShade() => ExtractMod(ModIdentifiers.ReShade, ScriptsDirectoryPath);

    #endregion

    #region UMod Methods

    /// <summary>
    /// Downloads the UMod mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadUMod(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.UMod, progressCallback);

    /// <summary>
    /// Installs the UMod mod by extracting it to the designated UMod location.
    /// </summary>
    public static void InstallUMod()
    {
        Directory.CreateDirectory(FilePaths.UModLocation);
        ExtractMod(ModIdentifiers.UMod, FilePaths.UModLocation);
    }
    
    public static async Task UninstalluMod(bool deleteConfigFile = false)
    {
        Logger.Info<ModManager>("Uninstalling uMod");
        if (Directory.Exists(FilePaths.UModLocation))
        {
            Logger.Debug<ModManager>($"uMod folder found ({FilePaths.UModLocation})");
            Logger.Info<ModManager>("Deleting uMod folder");
            Directory.Delete(FilePaths.UModLocation, true);
        }
        else
        {
            Logger.Warning<ModManager>("uMod folder not found (Could be already uninstalled)");
        }
        if (Directory.Exists(FilePaths.ModsDirectory))
        {
            Logger.Info<ModManager>("Deleting Mods folder");
            Directory.Delete(FilePaths.ModsDirectory, true);
        }
        else
        {
            Logger.Warning<ModManager>("Mods folder not found (Could be already uninstalled)");
        }
        if (deleteConfigFile)
        {
            Logger.Info<ModManager>($"Deleting uMod config folder ({FilePaths.UModAppdata})");
            Directory.Delete(FilePaths.UModAppdata, true);
        }
        else
        {
            Logger.Info<ModManager>("Deleting game entry from uMod config file");
            await UModManager.RemoveGameFromAppdata(FilePaths.AltairExecutables.Values.ToList());
        }
    }


    #endregion

    #region Overhaul Methods

    /// <summary>
    /// Downloads the Overhaul mod asynchronously with progress reporting.
    /// </summary>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static Task DownloadOverhaul(Action<int> progressCallback) => DownloadModAsync(ModIdentifiers.Overhaul, progressCallback);

    /// <summary>
    /// Installs the Overhaul mod by extracting it to the dedicated overhaul directory.
    /// </summary>
    public static void InstallOverhaul()
    {
        Directory.CreateDirectory(OverhaulDirectoryPath);
        ExtractMod(ModIdentifiers.Overhaul, OverhaulDirectoryPath);
    }

    #endregion

    #region UMod Setup Methods

    /// <summary>
    /// Sets up UMod by configuring appdata and creating the save file with the overhaul mod.
    /// </summary>
    /// <returns>A task representing the asynchronous setup operation</returns>
    public static async Task SetupUMod()
    {
        await UModManager.SetupAppdata(FilePaths.AltairExecutables.Values.ToList());
        string[] uModMods = [FilePaths.OverhaulTpfFile];
        await UModManager.SetupSaveFile(FilePaths.AltairExecutables.Values.ToList(), "ac1.txt", uModMods);
    }

    #endregion

    #region Update Methods

    /// <summary>
    /// Downloads a specific mod by its identifier asynchronously with progress reporting.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier of the mod to download</param>
    /// <param name="progressCallback">A callback function to report download progress (0-100)</param>
    /// <returns>A task representing the asynchronous download operation</returns>
    public static async Task DownloadModByIdAsync(string modIdentifier, Action<int> progressCallback)
    {
        ModDefinition mod = ManifestService.GetMod(modIdentifier);
        Logger.Info<ModManager>($"Downloading {mod.Name}");

        DownloadManagerInstance.ProgressChanged += progressCallback;
        string savePath = Path.Combine(DownloadsDirectoryPath, Path.GetFileName(mod.Url));
        Logger.Debug<ModManager>($"Save path: {savePath}");

        try
        {
            await DownloadManagerInstance.DownloadFileAsync(mod.Url, savePath);
        }
        catch (Exception ex)
        {
            Logger.Error<ModManager>($"Failed to download {mod.Name}");
            Logger.LogExceptionDetails<ModManager>(ex);
            throw new Exception($"Failed to download {mod.Name}", ex);
        }
        finally
        {
            DownloadManagerInstance.ProgressChanged -= progressCallback;
        }

        Logger.Info<ModManager>("Download complete");
    }

    /// <summary>
    /// Extracts a mod by its identifier to the appropriate location based on the mod type.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier of the mod to extract</param>
    /// <returns>A task representing the asynchronous extraction operation</returns>
    public static void ExtractModById(string modIdentifier)
    {
        ModDefinition mod = ManifestService.GetMod(modIdentifier);
        Logger.Info<ModManager>($"Extracting {mod.Name}");

        string archivePath = Path.Combine(DownloadsDirectoryPath, Path.GetFileName(mod.Url));
        Logger.Debug<ModManager>($"Archive location: {archivePath}");

        switch (modIdentifier)
        {
            case ModIdentifiers.AsiLoader:
                ExtractMod(ModIdentifiers.AsiLoader, AbsolutePath.BaseDirectory());
                Directory.CreateDirectory(ScriptsDirectoryPath);
                break;
            case ModIdentifiers.EaglePatch:
                ExtractMod(ModIdentifiers.EaglePatch, ScriptsDirectoryPath, [".ini", ".asi"]);
                break;
            case ModIdentifiers.AltairFix:
                ExtractMod(ModIdentifiers.AltairFix, ScriptsDirectoryPath, [".ini", ".asi"]);
                break;
            case ModIdentifiers.ReShade:
                ExtractMod(ModIdentifiers.ReShade, ScriptsDirectoryPath);
                break;
            case ModIdentifiers.UMod:
                Directory.CreateDirectory(FilePaths.UModLocation);
                ExtractMod(ModIdentifiers.UMod, FilePaths.UModLocation);
                break;
            case ModIdentifiers.Overhaul:
                Directory.CreateDirectory(OverhaulDirectoryPath);
                ExtractMod(ModIdentifiers.Overhaul, OverhaulDirectoryPath);
                break;
            default:
                throw new ArgumentException($"Unknown mod identifier: {modIdentifier}");
        }
    }

    #endregion

    /// <summary>
    /// Updates the installed mod version in the settings based on the manifest.
    /// This method can be called from the UI layer where services are accessible.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier of the mod</param>
    /// <param name="settings">The AltairSettings instance to update</param>
    public static void UpdateInstalledModVersion(string modIdentifier, AltairSettings settings)
    {
        try
        {
            ModDefinition mod = ManifestService.GetMod(modIdentifier);
            settings.UpdateInstalledModVersion(modIdentifier, mod.Version);
        }
        catch (Exception ex)
        {
            Logger.Error<ModManager>($"Failed to update installed mod version for {modIdentifier}");
            Logger.LogExceptionDetails<ModManager>(ex);
        }
    }
}