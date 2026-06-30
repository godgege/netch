using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class LogPage : Page
{
    public LogViewModel ViewModel { get; }

    public LogPage()
    {
        ViewModel = App.Services.GetRequiredService<LogViewModel>();
        InitializeComponent();

        var logPath = System.IO.Path.Combine(NetchAppContext.Current!.NetchDir, Netch.Constants.LogFile);
        ViewModel.StartWatching(logPath);
    }
}
