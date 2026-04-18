using System;
using System.IO;
using System.Threading.Tasks;
using AnimusReforged.Altair.Services.UI;
using AnimusReforged.Logging;
using AnimusReforged.Models.Altair;
using AnimusReforged.Mods.Altair;
using AnimusReforged.Mods.Core;
using AnimusReforged.Settings;
using AnimusReforged.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AnimusReforged.Altair.ViewModels.Pages;

public partial class WelcomePageViewModel : ViewModelBase
{
    // Variables
    private MainWindowViewModel _mainWindowViewModel { get; set; }
    private AltairSettings _settings { get; set; }
    private readonly IMessageBoxService _messageBoxService;

    [ObservableProperty] private string statusText = string.Empty;

    [ObservableProperty] private int progressBarValue;

    public WelcomePageViewModel()
    {
        _mainWindowViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _settings = App.Services.GetRequiredService<AltairSettings>();
        _messageBoxService = App.Services.GetRequiredService<IMessageBoxService>();
    }

    // Methods
    [RelayCommand]
    private async Task Install()
    {
        Logger.Info<WelcomePageViewModel>("Starting installation process");

        try
        {
            _mainWindowViewModel.DisableWindow = true;
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingModsList");
            Logger.Info<WelcomePageViewModel>("Initializing mod manager and downloading mods list");
            await ModManager.InitializeAsync();
            Logger.Info<WelcomePageViewModel>("Successfully downloaded mods list");

            // Install ASI Loader
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingAsiLoader");
            Logger.Info<WelcomePageViewModel>("Starting ASI Loader download");
            await ModManager.DownloadAsiLoader(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("ASI Loader download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingAsiLoader");
            Logger.Info<WelcomePageViewModel>("Installing ASI Loader");
            ModManager.InstallAsiLoader();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.AsiLoader, _settings);
            Logger.Info<WelcomePageViewModel>("ASI Loader installation completed");

            // Install EaglePatch
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingEaglePatch");
            Logger.Info<WelcomePageViewModel>("Starting EaglePatch download");
            await ModManager.DownloadEaglePatch(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("EaglePatch download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingEaglePatch");
            Logger.Info<WelcomePageViewModel>("Installing EaglePatch mod");
            ModManager.InstallEaglePatch();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.EaglePatch, _settings);
            Logger.Info<WelcomePageViewModel>("EaglePatch installation completed");

            // Install AltairFix
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingAltairFix");
            Logger.Info<WelcomePageViewModel>("Starting AltairFix download");
            await ModManager.DownloadAltairFix(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("AltairFix download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingAltairFix");
            Logger.Info<WelcomePageViewModel>("Installing AltairFix");
            ModManager.InstallAltairFix();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.AltairFix, _settings);
            Logger.Info<WelcomePageViewModel>("AltairFix installation completed");

            // Install ReShade
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingReShade");
            Logger.Info<WelcomePageViewModel>("Starting ReShade download");
            await ModManager.DownloadReShade(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("ReShade download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingReShade");
            Logger.Info<WelcomePageViewModel>("Installing ReShade");
            ModManager.InstallReShade();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.ReShade, _settings);
            Logger.Info<WelcomePageViewModel>("ReShade installation completed");

            // Install uMod
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingUMod");
            Logger.Info<WelcomePageViewModel>("Starting uMod download");
            await ModManager.DownloadUMod(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("uMod download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingUMod");
            Logger.Info<WelcomePageViewModel>("Installing uMod");
            ModManager.InstallUMod();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.UMod, _settings);
            Logger.Info<WelcomePageViewModel>("uMod installation completed");

            // Install Overhaul
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.DownloadingOverhaul");
            Logger.Info<WelcomePageViewModel>("Starting Overhaul mod download");
            await ModManager.DownloadOverhaul(progress => ProgressBarValue = progress);
            Logger.Info<WelcomePageViewModel>("Overhaul mod download completed");

            StatusText = LocalizationHelper.GetText("WelcomePage.Status.InstallingOverhaul");
            Logger.Info<WelcomePageViewModel>("Installing Overhaul mod");
            ModManager.InstallOverhaul();
            ModManager.UpdateInstalledModVersion(ModIdentifiers.Overhaul, _settings);
            Logger.Info<WelcomePageViewModel>("Overhaul mod installation completed");

            // Setup uMod and Overhaul
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.SettingUpUMod");
            Logger.Info<WelcomePageViewModel>("Setting up uMod and Overhaul mod");
            await ModManager.SetupUMod();
            Logger.Info<WelcomePageViewModel>("uMod and Overhaul setup completed");

            // Applying 4GB Patch (Large Address Aware)
            StatusText = LocalizationHelper.GetText("WelcomePage.Status.Applying4gbPatch");
            Logger.Info<WelcomePageViewModel>("Applying 4GB Patch (Large Address Aware) to AssassinsCreed_Dx9.exe");
            Patcher.LargeAddressAwarePatch(FilePaths.AltairExecutables[ExecutableType.DX9]);
            Logger.Info<WelcomePageViewModel>("4GB Patch applied successfully");

            // Replace DX10 executable with patched DX9 executable
            if (File.Exists(FilePaths.AltairExecutables[ExecutableType.DX10]))
            {
                Patcher.CreateBackup(FilePaths.AltairExecutables[ExecutableType.DX10]);
            }
            File.Copy(FilePaths.AltairExecutables[ExecutableType.DX9], FilePaths.AltairExecutables[ExecutableType.DX10], true);

            // Delete the downloads directory recursively
            if (Directory.Exists(FilePaths.DownloadsDirectory))
            {
                Logger.Info<WelcomePageViewModel>("Deleting downloads directory");
                Directory.Delete(FilePaths.DownloadsDirectory, true);
                Logger.Info<WelcomePageViewModel>("Downloads directory deleted successfully");
            }
            else
            {
                Logger.Info<WelcomePageViewModel>("Downloads directory does not exist, skipping deletion");
            }

            _settings.Settings.Setup.Completed = true;
            await _settings.SaveSettingsAsync();
            Logger.Info<WelcomePageViewModel>("Settings saved and setup marked as completed");

            _mainWindowViewModel.DisableWindow = false;
            Logger.Info<WelcomePageViewModel>("Installation completed successfully, showing success message");
            await _messageBoxService.ShowInfoAsync(
                LocalizationHelper.GetText("WelcomePage.MessageBox.InstallationCompletedTitle"),
                LocalizationHelper.GetText("WelcomePage.MessageBox.InstallationCompletedMessage"));
        }
        catch (Exception ex)
        {
            Logger.Error<WelcomePageViewModel>($"Installation failed with error: {ex.Message}");
            Logger.Error<WelcomePageViewModel>($"Stack trace: {ex.StackTrace}");
            _mainWindowViewModel.DisableWindow = false;
            await _messageBoxService.ShowErrorAsync(
                LocalizationHelper.GetText("WelcomePage.MessageBox.InstallationFailedTitle"),
                string.Format(LocalizationHelper.GetText("WelcomePage.MessageBox.InstallationFailedMessage"), ex.Message));
        }

        Logger.Info<WelcomePageViewModel>("Finished installation process");
    }
}