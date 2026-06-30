using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.App.Models;
using Netch.App.Services;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models.Modes.ProcessMode;
using Netch.Servers;
using Netch.Utils;
using Serilog;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Netch.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NetchAppContext _appContext;
    private readonly MainController _mainController;
    private readonly Configuration _configuration;
    private List<InstalledApp> _allApps = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
    private State _currentState = State.Waiting;

    [ObservableProperty]
    private string _proxyHost = "127.0.0.1";

    [ObservableProperty]
    private string _proxyPort = "";

    [ObservableProperty]
    private string _statusText = "Waiting for command";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLogExpanded;

    public ObservableCollection<InstalledApp> FilteredApps { get; } = new();
    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();
    public ObservableCollection<string> LogEntries => App.UiLogSink?.LogEntries ?? new();

    public bool CanStartStop => CurrentState is State.Waiting or State.Stopped or State.Started;

    public string ControlButtonText => CurrentState switch
    {
        State.Waiting or State.Stopped => "Start",
        State.Started => "Stop",
        State.Starting => "Starting...",
        State.Stopping => "Stopping...",
        _ => ""
    };

    public MainViewModel(NetchAppContext appContext, MainController mainController, Configuration configuration)
    {
        _appContext = appContext;
        _mainController = mainController;
        _configuration = configuration;
    }

    public void Initialize()
    {
        var settings = _appContext.Settings;
        ProxyHost = settings.LiteProxyHost;
        ProxyPort = settings.LiteProxyPort;

        _allApps = AppDiscoveryService.DiscoverInstalledApps();

        var savedPaths = settings.SelectedAppPaths;
        foreach (var app in _allApps)
        {
            if (savedPaths.Contains(app.InstallPath, StringComparer.OrdinalIgnoreCase))
            {
                app.IsSelected = true;
                var group = new ProcessGroup { GroupName = app.Name };
                ScanDirectoryInto(app.InstallPath, group);
                if (group.Processes.Count > 0)
                    ProcessGroups.Add(group);
            }
        }

        ApplyFilter();
    }

    partial void OnCurrentStateChanged(State value)
    {
        OnPropertyChanged(nameof(CanStartStop));
        OnPropertyChanged(nameof(ControlButtonText));
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredApps.Clear();
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var app in filtered)
            FilteredApps.Add(app);
    }

    public void OnAppSelectionChanged(InstalledApp app)
    {
        if (app.IsSelected)
        {
            var group = new ProcessGroup { GroupName = app.Name };
            ScanDirectoryInto(app.InstallPath, group);
            if (group.Processes.Count > 0)
                ProcessGroups.Add(group);
        }
        else
        {
            var group = ProcessGroups.FirstOrDefault(g => g.GroupName == app.Name);
            if (group != null)
                ProcessGroups.Remove(group);
        }

        _ = SaveSelectionAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStartStop))]
    private async Task StartStopAsync()
    {
        if (CurrentState is State.Waiting or State.Stopped)
            await StartAsync();
        else if (CurrentState == State.Started)
            await StopAsync();
    }

    private async Task StartAsync()
    {
        if (!ushort.TryParse(ProxyPort, out var port) || port == 0)
        {
            StatusText = "Please enter a valid port";
            return;
        }

        StatusText = "Testing proxy connectivity...";
        var ip = await ResolveHostAsync(ProxyHost);
        if (ip == null)
        {
            Log.Warning("Cannot resolve host {Host}", ProxyHost);
            StatusText = $"Cannot resolve host: {ProxyHost}";
            return;
        }

        var delay = await Netch.Utils.Utils.TCPingAsync(ip, port, 2000);
        if (delay >= 2000)
        {
            Log.Warning("Proxy {Host}:{Port} is not reachable (timeout)", ProxyHost, port);
            StatusText = $"Proxy not reachable: {ProxyHost}:{port} (timeout)";
            return;
        }

        Log.Information("Proxy {Host}:{Port} connected, delay {Delay}ms", ProxyHost, port, delay);

        var allProcesses = ProcessGroups.SelectMany(g => g.Processes).ToList();
        if (allProcesses.Count == 0)
        {
            StatusText = "No processes added";
            return;
        }

        var server = new Socks5Server(ProxyHost, port);

        var mode = new Redirector
        {
            FilterIntranet = true,
            FilterLoopback = false
        };
        foreach (var proc in allProcesses)
            mode.Handle.Add(proc.FileName);

        Log.Information("Starting redirector → {Host}:{Port}, processes: {List}",
            ProxyHost, port, string.Join(", ", allProcesses.Select(p => p.FileName)));

        try
        {
            CurrentState = State.Starting;
            StatusText = "Starting...";
            await _mainController.StartAsync(server, mode);
            CurrentState = State.Started;
            StatusText = "Running";
            Log.Information("Redirector started successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Redirector start failed");
            StatusText = ex.Message;
            CurrentState = State.Stopped;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            CurrentState = State.Stopping;
            StatusText = "Stopping...";
            await _mainController.StopAsync();
            CurrentState = State.Stopped;
            StatusText = "Stopped";
            Log.Information("Redirector stopped");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Redirector stop failed");
            StatusText = ex.Message;
            CurrentState = State.Stopped;
        }
    }

    [RelayCommand]
    private async Task AddToGroupAsync(ProcessGroup group)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null)
            return;

        ScanDirectoryInto(folder.Path, group);
    }

    [RelayCommand]
    private async Task AddExeToGroupAsync(ProcessGroup group)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        var path = file.Path;
        if (group.Processes.All(p => !p.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            group.Processes.Add(new ProcessEntry { FullPath = path });
    }

    [RelayCommand]
    private void RemoveGroup(ProcessGroup group)
    {
        ProcessGroups.Remove(group);
        var app = _allApps.FirstOrDefault(a => a.Name == group.GroupName);
        if (app != null)
            app.IsSelected = false;

        _ = SaveSelectionAsync();
    }

    private void ScanDirectoryInto(string path, ProcessGroup group)
    {
        try
        {
            foreach (var exe in EnumerateExesSafe(path))
            {
                if (group.Processes.All(p => !p.FullPath.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                    group.Processes.Add(new ProcessEntry { FullPath = exe });
            }
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = $"Access denied: {path}";
        }
    }

    private static IEnumerable<string> EnumerateExesSafe(string root)
    {
        Queue<string> dirs = new();
        dirs.Enqueue(root);

        while (dirs.Count > 0)
        {
            var dir = dirs.Dequeue();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, "*.exe"); }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }

            foreach (var f in files)
                yield return f;

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(dir))
                    dirs.Enqueue(subDir);
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private async Task SaveSelectionAsync()
    {
        var settings = _appContext.Settings;
        settings.LiteProxyHost = ProxyHost;
        settings.LiteProxyPort = ProxyPort;
        settings.SelectedAppPaths = _allApps
            .Where(a => a.IsSelected)
            .Select(a => a.InstallPath)
            .ToList();

        await _configuration.SaveAsync();
    }

    private static async Task<IPAddress?> ResolveHostAsync(string host)
    {
        if (IPAddress.TryParse(host, out var ip))
            return ip;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            return addresses.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
