using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Netch.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Netch";
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(MainPage));
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
