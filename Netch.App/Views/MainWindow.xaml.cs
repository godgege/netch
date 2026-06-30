using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Netch;

namespace Netch.App.Views;

public sealed partial class MainWindow : Window
{
    private TaskbarIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Netch";
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(MainPage));

        SetupTrayIcon();
        this.AppWindow.Closing += OnClosing;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Netch"
        };

        var menu = new MenuFlyout();

        var showItem = new MenuFlyoutItem { Text = "Show" };
        showItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(showItem);

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            this.Close();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextFlyout = menu;
        _trayIcon.TrayIconLeftMouseUp += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        this.Activate();
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
            presenter.Restore();
    }

    private void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        var settings = NetchAppContext.Current?.Settings;
        if (settings != null && !settings.ExitWhenClosed)
        {
            args.Cancel = true;
            var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            presenter?.Minimize();
        }
        else
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item)
            return;

        var tag = item.Tag?.ToString();
        var pageType = tag switch
        {
            "MainPage" => typeof(MainPage),
            "ServersPage" => typeof(ServersPage),
            "ModesPage" => typeof(ModesPage),
            "SubscriptionPage" => typeof(SubscriptionPage),
            "LogPage" => typeof(LogPage),
            "SettingsPage" => typeof(SettingsPage),
            "AboutPage" => typeof(AboutPage),
            _ => null
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
