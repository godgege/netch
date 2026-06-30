using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.App.Models;
using Netch.App.Services;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models.Modes.ProcessMode;
using Netch.Servers;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Netch.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NetchAppContext _appContext;
    private readonly MainController _mainController;
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

    public ObservableCollection<InstalledApp> FilteredApps { get; } = new();
    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();

    public bool CanStartStop => CurrentState is State.Waiting or State.Stopped or State.Started;

    public string ControlButtonText => CurrentState switch
    {
        State.Waiting or State.Stopped => "Start",
        State.Started => "Stop",
        State.Starting => "Starting...",
        State.Stopping => "Stopping...",
        _ => ""
    };

    public MainViewModel(NetchAppContext appContext, MainController mainController)
    {
        _appContext = appContext;
        _mainController = mainController;
    }

    public void Initialize()
    {
        _allApps = AppDiscoveryService.DiscoverInstalledApps();
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

        try
        {
            CurrentState = State.Starting;
            StatusText = "Starting...";
            await _mainController.StartAsync(server, mode);
            CurrentState = State.Started;
            StatusText = "Running";
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
            StatusText = "Stopping...";
            await _mainController.StopAsync();
            CurrentState = State.Stopped;
            StatusText = "Stopped";
        }
        catch (Exception ex)
        {
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
    private void RemoveGroup(ProcessGroup group)
    {
        ProcessGroups.Remove(group);
        var app = _allApps.FirstOrDefault(a => a.Name == group.GroupName);
        if (app != null)
            app.IsSelected = false;
    }

    private void ScanDirectoryInto(string path, ProcessGroup group)
    {
        try
        {
            var exes = Directory.EnumerateFiles(path, "*.exe", SearchOption.AllDirectories);
            foreach (var exe in exes)
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
}
