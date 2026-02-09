using AnimusReforged.Altair.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using AnimusReforged.Settings;
using AnimusReforged.Utilities;

namespace AnimusReforged.Altair.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string windowTitle;
    [ObservableProperty] private bool disableWindow;
    [ObservableProperty] private bool hasUpdateNotifications;
    [ObservableProperty] private string updateNotificationMessage;

    private readonly IUpdateNotificationService _updateNotificationService;
    private Timer? _autoCloseTimer;

    public MainWindowViewModel()
    {
        AltairSettings settings = App.Services.GetRequiredService<AltairSettings>();
        WindowTitle = string.Format(LocalizationHelper.GetText("MainWindow.Title"), settings.Settings.GetVersion());
        disableWindow = false;
        hasUpdateNotifications = false;
        updateNotificationMessage = string.Empty;

        // Get the update notification service
        _updateNotificationService = App.Services.GetRequiredService<IUpdateNotificationService>();

        // Subscribe to update notifications
        _updateNotificationService.UpdatesChanged += OnUpdatesChanged;
    }

    private void OnUpdatesChanged(object? sender, EventArgs e)
    {
        bool hasUpdates = _updateNotificationService.HasUpdates;
        int updateCount = _updateNotificationService.UpdateCount;

        HasUpdateNotifications = hasUpdates;
        UpdateNotificationMessage = hasUpdates
            ? $"Update available: {updateCount} mod(s) can be updated. Go to Manage page to update."
            : string.Empty;

        // Set up the auto-close timer if there are notifications
        if (hasUpdates)
        {
            SetupAutoCloseTimer();
        }
    }

    private void SetupAutoCloseTimer()
    {
        // Cancel any existing timer
        _autoCloseTimer?.Dispose();

        // Create a new timer that closes the notification after 5 seconds
        _autoCloseTimer = new Timer(CloseNotificationCallback, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    private void CloseNotificationCallback(object? state)
    {
        // Switch to the UI thread to update the property
        // Only close the InfoBar, don't clear the actual update notifications
        Dispatcher.UIThread.Post(() =>
        {
            HasUpdateNotifications = false;
            UpdateNotificationMessage = string.Empty;
        }, DispatcherPriority.Normal);
    }

    public void ClearUpdateNotifications()
    {
        // Cancel the auto-close timer if it exists
        _autoCloseTimer?.Dispose();
        _autoCloseTimer = null;

        HasUpdateNotifications = false;
        UpdateNotificationMessage = string.Empty;
    }
}