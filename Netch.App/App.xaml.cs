using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Netch.App.Services;
using Netch.App.ViewModels;
using Netch.App.Views;
using Netch.Controllers;
using Netch.Interfaces;
using Netch.Services;
using Netch.Utils;
using Serilog;

namespace Netch.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MainWindow MainWindow { get; private set; } = null!;

    private readonly NetchAppContext _appContext;

    public App()
    {
        InitializeComponent();

        _appContext = InitializeAppContext();
        NetchAppContext.Current = _appContext;

        Services = ConfigureServices(_appContext);

        InitializeLogging();
        LoadConfiguration();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static NetchAppContext InitializeAppContext()
    {
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var dir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;

        return new NetchAppContext
        {
            NetchDir = dir,
            NetchExecutable = exePath
        };
    }

    private static IServiceProvider ConfigureServices(NetchAppContext appContext)
    {
        var services = new ServiceCollection();

        // Core context
        services.AddSingleton(appContext);

        // Core services
        services.AddSingleton<IStatusReporter, StatusReporterService>();
        services.AddSingleton<INotificationService, NotificationServiceImpl>();
        services.AddSingleton<IModeListManager, ModeListManagerService>();
        services.AddSingleton<IWindowActivator, WindowActivatorService>();
        services.AddSingleton<MainController>();
        services.AddSingleton<ModeService>();
        services.AddSingleton<Configuration>();
        services.AddSingleton<DelayTestHelper>();
        services.AddSingleton<Bandwidth>();
        services.AddSingleton<SubscriptionUtil>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SubscriptionViewModel>();
        services.AddTransient<LogViewModel>();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        return services.BuildServiceProvider();
    }

    private void InitializeLogging()
    {
        var logPath = Path.Combine(_appContext.NetchDir, Constants.LogFile);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(logPath,
                outputTemplate: Constants.OutputTemplate,
                rollingInterval: RollingInterval.Day))
            .WriteTo.Console(outputTemplate: Constants.OutputTemplate)
            .CreateLogger();
    }

    private void LoadConfiguration()
    {
        var config = Services.GetRequiredService<Configuration>();
        config.LoadAsync().Wait();

        i18N.Load(_appContext.Settings.Language);
        ModeHelper.ModeBaseDirectory = Path.Combine(_appContext.NetchDir, "mode");
    }
}
