using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using AnimusReforged.Logging;
using AnimusReforged.Utilities;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;

namespace AnimusReforged.Altair.ViewModels.Pages;

public partial class CreditsPageViewModel : ViewModelBase
{
    public class CreditItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Symbol Icon { get; set; } = Symbol.Link;
    }

    public ObservableCollection<CreditItem> Credits { get; } =
    [
        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.uMod.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.uMod.Description"),
            Url = "https://code.google.com/archive/p/texmod/",
            Icon = Symbol.Wrench
        },

        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.Overhaul.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.Overhaul.Description"),
            Url = "https://www.moddb.com/mods/assassins-creed-2014-overhaul",
            Icon = Symbol.Sparkle
        },

        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.EaglePatch.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.EaglePatch.Description"),
            Url = "https://github.com/Sergeanur/EaglePatch",
            Icon = Symbol.Code
        },

        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.ReShadeTool.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.ReShadeTool.Description"),
            Url = "https://reshade.me/",
            Icon = Symbol.ColorBackground
        },

        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.ReShadePreset.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.ReShadePreset.Description"),
            Url = "https://steamcommunity.com/sharedfiles/filedetails/?id=2957930769",
            Icon = Symbol.PaintBrush
        },

        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.Background.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.Background.Description"),
            Url = "https://www.nexusmods.com/users/70787823",
            Icon = Symbol.ImageSparkle
        },
        
        new CreditItem
        {
            Title = LocalizationHelper.GetText("CreditsPage.Icon.Title"),
            Description = LocalizationHelper.GetText("CreditsPage.Icon.Description"),
            Url = "https://www.deviantart.com/artef4ct/art/Animus-Logo-by-ArteF4ct-351074884",
            Icon = Symbol.Icons
        }
    ];

    [RelayCommand]
    private void OpenLink(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Logger.Warning<CreditsPageViewModel>("No URL provided to open");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error<CreditsPageViewModel>($"There was an error opening the link: {url}");
            Logger.LogExceptionDetails<CreditsPageViewModel>(ex);
        }
    }
}