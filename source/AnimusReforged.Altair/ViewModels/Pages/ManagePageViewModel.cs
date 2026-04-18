using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnimusReforged.Altair.Services;
using AnimusReforged.Models;
using AnimusReforged.Altair.Services.UI;
using AnimusReforged.Logging;
using AnimusReforged.Models.Altair;
using AnimusReforged.Mods.Altair;
using AnimusReforged.Mods.Core;
using AnimusReforged.Models.Mods;
using AnimusReforged.Settings;
using AnimusReforged.Settings.Core;
using AnimusReforged.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AnimusReforged.Altair.ViewModels.Pages;

public partial class ManagePageViewModel : ViewModelBase
{
    // Variables
    private MainWindowViewModel _mainWindowViewModel { get; set; }
    private AltairSettings _settings { get; set; }
    private readonly IMessageBoxService _messageBoxService;

    // Download Progress Bar
    [ObservableProperty] private bool isDownloading;
    [ObservableProperty] private string downloadStatus = LocalizationHelper.GetText("ManagePage.DownloadStatus.Waiting");
    [ObservableProperty] private string updateCheckStatus = LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.NoUpdates");
    [ObservableProperty] private int downloadProgress;

    // Update Checking
    [ObservableProperty] private ObservableCollection<UpdatableMod> updatableMods = [];
    [ObservableProperty] private bool hasUpdatableMods;

    // Redownload Mod Section
    [ObservableProperty] private ObservableCollection<string> availableMods = [];
    [ObservableProperty] private string? selectedMod;

    // Constructor
    public ManagePageViewModel()
    {
        _mainWindowViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _settings = App.Services.GetRequiredService<AltairSettings>();
        _messageBoxService = App.Services.GetRequiredService<IMessageBoxService>();

        IsDownloading = false;
        DownloadProgress = 0;
        HasUpdatableMods = false;

        // Initialize available mods with all known mod identifiers
        AvailableMods.Add(ModIdentifiers.AsiLoader);
        AvailableMods.Add(ModIdentifiers.EaglePatch);
        AvailableMods.Add(ModIdentifiers.AltairFix);
        AvailableMods.Add(ModIdentifiers.ReShade);
        AvailableMods.Add(ModIdentifiers.UMod);
        AvailableMods.Add(ModIdentifiers.Overhaul);
    }

    public void OnPageLoaded()
    {
        // Check if there are already known updates from the startup check
        IUpdateNotificationService updateService = App.Services.GetRequiredService<IUpdateNotificationService>();
        if (!updateService.HasUpdates || updateService.UpdatableMods.Count <= 0)
        {
            return;
        }

        // Populate the UpdatableMods collection with existing updates
        UpdatableMods.Clear();
        foreach (UpdatableMod mod in updateService.UpdatableMods)
        {
            UpdatableMods.Add(mod);
        }
        HasUpdatableMods = UpdatableMods.Count > 0;
        DownloadStatus = !HasUpdatableMods
            ? LocalizationHelper.GetText("ManagePage.DownloadStatus.NoUpdates")
            : string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.UpdatesAvailable"), UpdatableMods.Count);
        UpdateCheckStatus = !HasUpdatableMods
            ? LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.NoUpdates")
            : string.Format(LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.UpdatesAvailable"), UpdatableMods.Count);
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            Logger.Info<ManagePageViewModel>("Checking for mod updates");

            // Show download progress section while checking for updates
            IsDownloading = true;
            DownloadStatus = LocalizationHelper.GetText("ManagePage.DownloadStatus.CheckingUpdates");
            DownloadProgress = 0;

            // Initialize mod manager to load manifest
            await ModManager.InitializeAsync();
            DownloadProgress = 20;

            // Get the current manifest
            ModManifest manifest = await ManifestService.GetManifestAsync(ManifestType.Altair);
            DownloadProgress = 40;

            // Clear previous updatable mods
            UpdatableMods.Clear();

            // Check each mod for updates
            foreach (KeyValuePair<string, ModDefinition> modEntry in manifest.Mods)
            {
                string modId = modEntry.Key;
                ModDefinition mod = modEntry.Value;

                // Get the currently installed version
                string? installedVersion = _settings.GetInstalledModVersion(modId);

                // If the mod is installed and the version differs, it can be updated
                if (installedVersion != null && installedVersion != mod.Version)
                {
                    UpdatableMod updatableMod = new UpdatableMod(modId, mod.Name, installedVersion, mod.Version);
                    UpdatableMods.Add(updatableMod);
                }
            }

            DownloadProgress = 100;
            HasUpdatableMods = UpdatableMods.Count > 0;

            DownloadStatus = !HasUpdatableMods
                ? LocalizationHelper.GetText("ManagePage.DownloadStatus.NoUpdates")
                : string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.UpdatesAvailable"), UpdatableMods.Count);
            UpdateCheckStatus = !HasUpdatableMods
                ? LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.NoUpdates")
                : string.Format(LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.UpdatesAvailable"), UpdatableMods.Count);

            IsDownloading = false;
            await _messageBoxService.ShowInfoAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.UpdateCheckTitle"),
                DownloadStatus);
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>($"Error checking for updates: {ex.Message}");
            IsDownloading = false;
            DownloadStatus = LocalizationHelper.GetText("ManagePage.DownloadStatus.ErrorChecking");
            UpdateCheckStatus = LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.ErrorChecking");
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.UpdateCheckFailedTitle"),
                string.Format(LocalizationHelper.GetText("ManagePage.MessageBox.UpdateCheckErrorMessage"), ex.Message));
        }
    }

    [RelayCommand]
    private async Task UpdateMod(UpdatableMod? updatableMod)
    {
        if (updatableMod == null)
        {
            return;
        }

        try
        {
            Logger.Info<ManagePageViewModel>($"Updating mod: {updatableMod.Name}");

            // Set the mod as updating
            updatableMod.IsUpdating = true;

            // Show download progress section
            IsDownloading = true;
            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.UpdatingMod"), updatableMod.Name);
            DownloadProgress = 0;

            _mainWindowViewModel.DisableWindow = true;

            // Download the mod
            await ModManager.DownloadModByIdAsync(updatableMod.Id, progress => DownloadProgress = progress);

            // Extract and install the mod
            ModManager.ExtractModById(updatableMod.Id);

            // Update the installed version in settings
            ModManager.UpdateInstalledModVersion(updatableMod.Id, _settings);

            // Update the UI
            updatableMod.CurrentVersion = updatableMod.LatestVersion;
            UpdatableMods.Remove(updatableMod);

            HasUpdatableMods = UpdatableMods.Count > 0;
            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.ModUpdatedSuccessfully"), updatableMod.Name);

            // Update the update check status to reflect the new count of remaining updatable mods
            UpdateCheckStatus = !HasUpdatableMods
                ? LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.NoUpdates")
                : string.Format(LocalizationHelper.GetText("ManagePage.UpdateCheckStatus.UpdatesAvailable"), UpdatableMods.Count);

            DownloadProgress = 100;

            // Hide progress section after a short delay
            await _settings.SaveSettingsAsync();
            IsDownloading = false;
            _mainWindowViewModel.DisableWindow = false;
            // Show success message
            await _messageBoxService.ShowInfoAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.UpdateSuccessfulTitle"),
                string.Format(LocalizationHelper.GetText("ManagePage.MessageBox.UpdateSuccessfulMessage"), updatableMod.Name, updatableMod.LatestVersion));
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>($"Error updating mod {updatableMod.Name}: {ex.Message}");
            IsDownloading = false;
            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.ErrorUpdating"), updatableMod.Name);
            _mainWindowViewModel.DisableWindow = false;
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.UpdateFailedTitle"),
                string.Format(LocalizationHelper.GetText("ManagePage.MessageBox.UpdateFailedMessage"), updatableMod.Name, ex.Message));
        }
        finally
        {
            updatableMod.IsUpdating = false;
        }
    }

    [RelayCommand]
    private async Task RedownloadSelectedMod()
    {
        if (string.IsNullOrEmpty(SelectedMod))
        {
            await _messageBoxService.ShowWarningAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.NoModSelectedTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.NoModSelectedMessage"));
            return;
        }

        try
        {
            Logger.Info<ManagePageViewModel>($"Redownloading mod: {SelectedMod}");

            // Show download progress section
            IsDownloading = true;
            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.RedownloadingMod"), ModIdentifiers.GetModName(SelectedMod));
            DownloadProgress = 0;

            _mainWindowViewModel.DisableWindow = true;

            await ModManager.InitializeAsync();

            // Download the selected mod
            await ModManager.DownloadModByIdAsync(SelectedMod, progress => DownloadProgress = progress);

            // Extract and install the mod
            ModManager.ExtractModById(SelectedMod);

            // Update the installed version in settings
            ModManager.UpdateInstalledModVersion(SelectedMod, _settings);

            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.ModRedownloadedSuccessfully"), ModIdentifiers.GetModName(SelectedMod));
            DownloadProgress = 100;

            // Hide progress section after a short delay
            await _settings.SaveSettingsAsync();
            IsDownloading = false;
            _mainWindowViewModel.DisableWindow = false;

            // Show success message
            await _messageBoxService.ShowInfoAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.RedownloadSuccessfulTitle"),
                string.Format(LocalizationHelper.GetText("ManagePage.MessageBox.RedownloadSuccessfulMessage"), ModIdentifiers.GetModName(SelectedMod)));
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>($"Error redownloading mod {SelectedMod}: {ex.Message}");
            IsDownloading = false;
            DownloadStatus = string.Format(LocalizationHelper.GetText("ManagePage.DownloadStatus.ErrorRedownloading"), ModIdentifiers.GetModName(SelectedMod));
            _mainWindowViewModel.DisableWindow = false;
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.RedownloadFailedTitle"),
                string.Format(LocalizationHelper.GetText("ManagePage.MessageBox.RedownloadFailedMessage"), ModIdentifiers.GetModName(SelectedMod), ex.Message));
        }
        finally
        {
            SelectedMod = null;
        }
    }

    [RelayCommand]
    private async Task FixUModPaths()
    {
        Logger.Info<ManagePageViewModel>("Redoing uMod paths");
        try
        {
            await UModManager.SetupAppdata(FilePaths.AltairExecutables.Values.ToList());
        }
        catch (ArgumentException)
        {
            Logger.Error<ManagePageViewModel>($"Invalid game executable path");
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.InvalidExecutablePathMessage"));
        }
        try
        {
            await UModManager.SetupSaveFile(FilePaths.AltairExecutables.Values.ToList(), "ac1.txt");
        }
        catch (ArgumentException)
        {
            Logger.Error<ManagePageViewModel>($"Invalid game executable path");
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.InvalidExecutablePathMessage"));
        }
        await _messageBoxService.ShowInfoAsync(
            LocalizationHelper.GetText("ManagePage.MessageBox.SuccessTitle"),
            LocalizationHelper.GetText("ManagePage.MessageBox.UmodPathsSuccessMessage"));
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        // Confirmation
        bool uninstallConfirmation = await _messageBoxService.ShowConfirmationAsync(
            LocalizationHelper.GetText("ManagePage.MessageBox.ConfirmationTitle"),
            LocalizationHelper.GetText("ManagePage.MessageBox.UninstallConfirmationMessage"));
        if (!uninstallConfirmation)
        {
            Logger.Info<ManagePageViewModel>("Uninstallation cancelled");
            return;
        }

        // Uninstall mods
        try
        {
            Logger.Info<ManagePageViewModel>("Uninstalling mods");
            ModManager.UninstallAsiLoader();
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>("Uninstalling ASI Loader & Mods failed");
            Logger.LogExceptionDetails<ManagePageViewModel>(ex);
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.UninstallAsiLoaderFailedMessage"));
            return;
        }

        // Uninstall uMod
        bool deleteuModConfig = await _messageBoxService.ShowConfirmationAsync(
            LocalizationHelper.GetText("ManagePage.MessageBox.ConfirmationTitle"),
            LocalizationHelper.GetText("ManagePage.MessageBox.UninstallUmodConfirmationMessage"));
        try
        {
            await ModManager.UninstalluMod(deleteuModConfig);
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>("Uninstalling uMod failed");
            Logger.LogExceptionDetails<ManagePageViewModel>(ex);
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.UninstallUmodFailedMessage"));
            return;
        }

        // Restore original executables
        try
        {
            // Restore DX9 executable
            if (File.Exists(FilePaths.AltairExecutables[ExecutableType.DX9] + ".bak"))
            {
                Logger.Info<ManagePageViewModel>("Restoring the original game executable without LAA patch applied");
                File.Copy(FilePaths.AltairExecutables[ExecutableType.DX9] + ".bak", FilePaths.AltairExecutables[ExecutableType.DX9], true);
                File.Delete(FilePaths.AltairExecutables[ExecutableType.DX9] + ".bak");
            }
            else
            {
                Logger.Warning<ManagePageViewModel>("Couldn't find backup of the original game executable");
            }

            // Restore DX10 executable
            if (File.Exists(FilePaths.AltairExecutables[ExecutableType.DX10] + ".bak"))
            {
                Logger.Info<ManagePageViewModel>("Restoring the original game executable without LAA patch applied");
                File.Copy(FilePaths.AltairExecutables[ExecutableType.DX10] + ".bak", FilePaths.AltairExecutables[ExecutableType.DX10], true);
                File.Delete(FilePaths.AltairExecutables[ExecutableType.DX10] + ".bak");
            }
            else
            {
                Logger.Warning<ManagePageViewModel>("Couldn't find backup of the original game executable");
            }
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>("Restoring the original game executable failed");
            Logger.LogExceptionDetails<ManagePageViewModel>(ex);
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.RestoreExecutableFailedMessage"));
            return;
        }

        // Resetting config file
        try
        {
            _settings.Settings.Setup = new SetupSettings();
            _settings.Settings.InstalledModVersions = new Dictionary<string, string>();
            await _settings.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error<ManagePageViewModel>("Resetting the configuration file failed");
            Logger.LogExceptionDetails<ManagePageViewModel>(ex);
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("ManagePage.MessageBox.FailureTitle"),
                LocalizationHelper.GetText("ManagePage.MessageBox.ResetConfigFailedMessage"));
            return;
        }
        await _messageBoxService.ShowInfoAsync(
            LocalizationHelper.GetText("ManagePage.MessageBox.SuccessTitle"),
            LocalizationHelper.GetText("ManagePage.MessageBox.UninstallSuccessMessage"));

        // Close the launcher
        Environment.Exit(0);
    }
}