using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Netch.App.Models;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Initialize();
        DispatcherQueue.TryEnqueue(() => PortBox.Focus(FocusState.Programmatic));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSettings();
        ViewModel.Dispose();
    }

    private void AppCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: InstalledApp app })
            ViewModel.OnAppSelectionChanged(app);
    }

    private void AddToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProcessGroup group })
            ViewModel.AddToGroupCommand.Execute(group);
    }

    private void AddExeToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProcessGroup group })
            ViewModel.AddExeToGroupCommand.Execute(group);
    }

    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProcessGroup group })
            ViewModel.RemoveGroupCommand.Execute(group);
    }
}
