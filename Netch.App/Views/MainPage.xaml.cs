using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
    }
}
