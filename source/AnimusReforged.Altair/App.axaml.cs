using System;
using System.Linq;
using System.Threading.Tasks;
using AnimusReforged.Altair.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AnimusReforged.Altair.Views;
using AnimusReforged.Bridge;
using AnimusReforged.Logging;
using AnimusReforged.Settings;
using AnimusReforged.Utilities;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AnimusReforged.Altair;

public partial class App : Application
{
    // Properties
    /// <summary>
    /// Desktop instance
    /// </summary>
    public static IClassicDesktopStyleApplicationLifetime? Desktop = Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    /// <summary>
    /// Main Window instance
    /// </summary>
    public static Window? MainWindow => Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;

    /// <summary>
    /// DI Services
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    // Functions
    public override void Initialize()
    {
        Logger.Debug<App>("Initializing Avalonia application");
        AvaloniaXamlLoader.Load(this);
        Logger.Debug<App>("Avalonia XAML loaded successfully");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Desktop is { } desktop)
        {
            // Disable Avalonia validation
            Logger.Trace<App>("Disabling Avalonia data annotation validation");
            DisableAvaloniaDataAnnotationValidation();

            // Configure services
            Logger.Debug<App>("Configuring dependency injection services");
            try
            {
                Services = ServiceConfigurator.ConfigureServices();
                Logger.Info<App>("Services configured successfully");
            }
            catch (Exception ex)
            {
                Logger.Error<App>("Failed to configure services");
                Logger.LogExceptionDetails<App>(ex);
                throw;
            }

            // Initial loading of settings
            AltairSettings settings = Services.GetRequiredService<AltairSettings>();
            settings.SaveSettings();

            // Localization initialization
            LocalizationHelper.Initialize("avares://AnimusReforged.Altair/Resources/Language/");
            LocalizationHelper.LoadLanguage(settings.Settings.Core.Language);

            // Get MainWindow
            Logger.Debug<App>("Resolving MainWindow from services");
            MainWindow mainWindow = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            // Wire up window events
            mainWindow.Opened += (_, _) =>
            {
                Logger.Info<App>("Launching AnimusReforged Altair");
                Logger.Info<App>($"Version: v{settings.Settings.GetVersion()}");
                Logger.Debug<App>("Main window opened");
            };

            mainWindow.Closing += (_, e) =>
            {
                Logger.Info<App>("Main window closing");
                Logger.Debug<App>("Flushing logs before shutdown");
                Logger.Flush();
            };

            // Application exit handler
            desktop.Exit += (_, _) =>
            {
                Logger.Info<App>("Closing AnimusReforged Altair");
                Logger.Debug<App>("Stopping l1l-@p3p bridge");
                BridgeServer.Instance?.Stop();
                Logger.Debug<App>("Shutting down logger");
                Logger.Shutdown();
            };

            // Global exception handlers
            Logger.Trace<App>("Registering global exception handlers");

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                Logger.Error<App>("Unobserved task exception occurred");
                HandleFatalException(args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                bool isTerminating = args.IsTerminating;
                Logger.Error<App>($"Unhandled exception in AppDomain (Terminating: {isTerminating})");

                if (args.ExceptionObject is Exception ex)
                {
                    HandleFatalException(ex);
                }
                else
                {
                    Logger.Error<App>($"Non-exception object thrown: {args.ExceptionObject?.GetType().FullName ?? "null"}");
                }
            };

            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                args.Handled = true;
                Logger.Error<App>("Unhandled exception on UI thread");
                HandleFatalException(args.Exception);
            };

            Logger.Debug<App>("Setting main window");
            desktop.MainWindow = mainWindow;

            // Start the l1l-@p3p bridge so the overlay can talk to the launcher/game
            Logger.Info<App>("Starting l1l-@p3p bridge");
            BridgeServer.StartDefault();

            Logger.Info<App>("Application initialization completed successfully");

            ArgumentChecker();
        }
        else
        {
            Logger.Warning<App>("Application is not running as desktop application");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void HandleFatalException(Exception ex)
    {
        try
        {
            Logger.Error<App>("=== Fatal Exception Encountered ===");
            Logger.LogExceptionDetails<App>(ex, includeEnvironmentInfo: true);

            // Ensure logs are written before potential crash
            Logger.Flush();
        }
        catch
        {
            // If logging fails, we can't do much about it
            // Just ensure we don't throw from the exception handler
        }
    }

    private void ArgumentChecker()
    {
        if (ArgumentParser.Contains(Desktop?.Args, "-skiplauncher"))
        {
            Desktop?.MainWindow?.Hide();
            Logger.Debug<App>("Launching the game without the UI");
            AltairSettings _settings = Services.GetRequiredService<AltairSettings>();
            Launcher.Altair.Launch(_settings.Settings.Tweaks.UMod);
            Logger.Debug<App>("Closing the application");
            Environment.Exit(0);
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}