using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class SubscriptionPage : Page
{
    public SubscriptionViewModel ViewModel { get; }

    public SubscriptionPage()
    {
        ViewModel = App.Services.GetRequiredService<SubscriptionViewModel>();
        InitializeComponent();
    }
}
