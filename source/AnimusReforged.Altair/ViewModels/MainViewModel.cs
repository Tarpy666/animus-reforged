using System;
using System.IO;
using System.Threading.Tasks;
using AnimusReforged.Altair.Services.UI;
using AnimusReforged.Logging;
using AnimusReforged.Models.Altair;
using AnimusReforged.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace AnimusReforged.Altair.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IMessageBoxService _messageBoxService;
    [ObservableProperty] private bool firstSetup;

    public MainViewModel()
    {
        _messageBoxService = App.Services.GetRequiredService<IMessageBoxService>();
    }

    public async Task CheckInstallation()
    {
        Logger.Info<MainViewModel>("Checking if the AnimusReforged has been placed next to the game executable");
        if (!File.Exists(FilePaths.AltairExecutables[ExecutableType.DX9]))
        {
            Logger.Error<MainViewModel>("Missing game executable");
            await _messageBoxService.ShowErrorAsync(LocalizationHelper.GetText("MainView.MissingExecutable.Title"),
                string.Format(LocalizationHelper.GetText("MainView.MissingExecutable.Text"), FilePaths.AltairExecutables[ExecutableType.DX9]));
            Environment.Exit(0);
        }
    }
}