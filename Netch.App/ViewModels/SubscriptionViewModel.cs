using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.Models;
using Netch.Utils;

namespace Netch.App.ViewModels;

public partial class SubscriptionViewModel : ObservableObject
{
    private readonly NetchAppContext _appContext;
    private readonly SubscriptionUtil _subscriptionUtil;
    private readonly Configuration _configuration;

    public ObservableCollection<Subscription> Subscriptions { get; } = new();

    [ObservableProperty] private string _remark = "";
    [ObservableProperty] private string _link = "";
    [ObservableProperty] private string _userAgent = "";
    [ObservableProperty] private Subscription? _selectedSubscription;

    public SubscriptionViewModel(NetchAppContext appContext, SubscriptionUtil subscriptionUtil, Configuration configuration)
    {
        _appContext = appContext;
        _subscriptionUtil = subscriptionUtil;
        _configuration = configuration;
        LoadSubscriptions();
    }

    private void LoadSubscriptions()
    {
        foreach (var sub in _appContext.Settings.Subscription)
            Subscriptions.Add(sub);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(Remark) || string.IsNullOrWhiteSpace(Link))
            return;

        var sub = new Subscription
        {
            Remark = Remark,
            Link = Link,
            UserAgent = UserAgent,
            Enable = true
        };

        _appContext.Settings.Subscription.Add(sub);
        Subscriptions.Add(sub);
        await _configuration.SaveAsync();

        Remark = "";
        Link = "";
        UserAgent = "";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSubscription == null) return;

        _appContext.Settings.Subscription.Remove(SelectedSubscription);
        Subscriptions.Remove(SelectedSubscription);
        await _configuration.SaveAsync();
    }

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        await _subscriptionUtil.UpdateServersAsync();
    }
}
