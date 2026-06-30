using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.Utils;

namespace Netch.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly NetchAppContext _appContext;
    private readonly Configuration _configuration;

    public SettingsViewModel(NetchAppContext appContext, Configuration configuration)
    {
        _appContext = appContext;
        _configuration = configuration;
        LoadSettings();
    }

    // General
    [ObservableProperty] private ushort _socks5LocalPort;
    [ObservableProperty] private bool _allowDevices;
    [ObservableProperty] private bool _serverTCPing;
    [ObservableProperty] private int _profileCount;
    [ObservableProperty] private int _detectionTick;
    [ObservableProperty] private int _startedPingInterval;
    [ObservableProperty] private string _stunServer = "";
    [ObservableProperty] private string _language = "";

    // Startup behavior
    [ObservableProperty] private bool _exitWhenClosed;
    [ObservableProperty] private bool _stopWhenExited;
    [ObservableProperty] private bool _startWhenOpened;
    [ObservableProperty] private bool _minimizeWhenStarted;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _checkUpdateWhenOpened;
    [ObservableProperty] private bool _checkBetaUpdate;
    [ObservableProperty] private bool _updateServersWhenOpened;

    private void LoadSettings()
    {
        var s = _appContext.Settings;
        Socks5LocalPort = s.Socks5LocalPort;
        AllowDevices = s.LocalAddress != "127.0.0.1";
        ServerTCPing = s.ServerTCPing;
        ProfileCount = s.ProfileCount;
        DetectionTick = s.DetectionTick;
        StartedPingInterval = s.StartedPingInterval;
        StunServer = s.STUN_Server;
        Language = s.Language;
        ExitWhenClosed = s.ExitWhenClosed;
        StopWhenExited = s.StopWhenExited;
        StartWhenOpened = s.StartWhenOpened;
        MinimizeWhenStarted = s.MinimizeWhenStarted;
        RunAtStartup = s.RunAtStartup;
        CheckUpdateWhenOpened = s.CheckUpdateWhenOpened;
        CheckBetaUpdate = s.CheckBetaUpdate;
        UpdateServersWhenOpened = s.UpdateServersWhenOpened;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _appContext.Settings;
        s.Socks5LocalPort = Socks5LocalPort;
        s.LocalAddress = AllowDevices ? "0.0.0.0" : "127.0.0.1";
        s.ServerTCPing = ServerTCPing;
        s.ProfileCount = ProfileCount;
        s.DetectionTick = DetectionTick;
        s.StartedPingInterval = StartedPingInterval;
        s.STUN_Server = StunServer;
        s.Language = Language;
        s.ExitWhenClosed = ExitWhenClosed;
        s.StopWhenExited = StopWhenExited;
        s.StartWhenOpened = StartWhenOpened;
        s.MinimizeWhenStarted = MinimizeWhenStarted;
        s.RunAtStartup = RunAtStartup;
        s.CheckUpdateWhenOpened = CheckUpdateWhenOpened;
        s.CheckBetaUpdate = CheckBetaUpdate;
        s.UpdateServersWhenOpened = UpdateServersWhenOpened;
        await _configuration.SaveAsync();
    }
}
