using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models;
using Netch.Models.Modes;
using Netch.Utils;

namespace Netch.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NetchAppContext _appContext;
    private readonly MainController _mainController;
    private readonly DelayTestHelper _delayTestHelper;

    public ObservableCollection<Server> Servers { get; } = new();
    public ObservableCollection<Mode> Modes { get; } = new();
    public ObservableCollection<Profile> Profiles { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    private State _currentState = State.Waiting;

    [ObservableProperty]
    private Server? _selectedServer;

    [ObservableProperty]
    private Mode? _selectedMode;

    [ObservableProperty]
    private string _statusText = "Waiting for command";

    [ObservableProperty]
    private string _downloadSpeed = "";

    [ObservableProperty]
    private string _uploadSpeed = "";

    [ObservableProperty]
    private string _usedBandwidth = "";

    [ObservableProperty]
    private bool _isBandwidthVisible;

    public bool CanStartStop => CurrentState is State.Waiting or State.Stopped or State.Started;

    public string ControlButtonText => CurrentState switch
    {
        State.Waiting or State.Stopped => i18N.Translate("Start"),
        State.Started => i18N.Translate("Stop"),
        State.Starting => i18N.Translate("Starting"),
        State.Stopping => i18N.Translate("Stopping"),
        _ => ""
    };

    public MainViewModel(NetchAppContext appContext, MainController mainController, DelayTestHelper delayTestHelper)
    {
        _appContext = appContext;
        _mainController = mainController;
        _delayTestHelper = delayTestHelper;

        LoadData();
    }

    private void LoadData()
    {
        foreach (var server in _appContext.Settings.Server)
            Servers.Add(server);

        foreach (var mode in _appContext.Modes)
            Modes.Add(mode);

        foreach (var profile in _appContext.Settings.Profiles)
            Profiles.Add(profile);
    }

    partial void OnCurrentStateChanged(State value)
    {
        OnPropertyChanged(nameof(CanStartStop));
        OnPropertyChanged(nameof(ControlButtonText));
    }

    [RelayCommand(CanExecute = nameof(CanStartStop))]
    private async Task StartStopAsync()
    {
        if (CurrentState is State.Waiting or State.Stopped)
        {
            await StartAsync();
        }
        else if (CurrentState == State.Started)
        {
            await StopAsync();
        }
    }

    private async Task StartAsync()
    {
        if (SelectedServer == null || SelectedMode == null)
            return;

        try
        {
            CurrentState = State.Starting;
            await _mainController.StartAsync(SelectedServer, SelectedMode);
            CurrentState = State.Started;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            CurrentState = State.Stopped;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            CurrentState = State.Stopping;
            await _mainController.StopAsync();
            CurrentState = State.Stopped;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            CurrentState = State.Stopped;
        }
    }

    [RelayCommand]
    private async Task TestDelayAsync()
    {
        if (SelectedServer != null)
        {
            await SelectedServer.PingAsync(_appContext.Settings.ServerTCPing);
        }
    }
}
