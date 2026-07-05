using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Netch.App.Models;
using Netch.App.Services;
using Netch.Enums;
using Netch.Utils;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Netch.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly NetchAppContext _appContext;
    private readonly LiteModeManager _liteModeManager;
    private readonly Configuration _configuration;
    private const string Socks5Protocol = "SOCKS5";
    private List<InstalledApp> _allApps = new();
    private CancellationTokenSource? _saveCts;

    [ObservableProperty]
    private string _proxyHost = string.Empty;

    [ObservableProperty]
    private string _proxyPort = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _useLocalhostOverride;

    public string SelectedProxyProtocol { get; set; } = Socks5Protocol;

    public IReadOnlyList<string> ProxyProtocols { get; } = [Socks5Protocol];

    public State CurrentState => _liteModeManager.CurrentState;

    public string StatusText => _liteModeManager.StatusText;

    public bool IsHostEditable => !UseLocalhostOverride;

    public ObservableCollection<InstalledApp> FilteredApps { get; } = new();

    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();

    public string SelectedAppsTitle => ProcessGroups.Count == 0 ? "已代理应用" : $"已代理应用 {ProcessGroups.Count}";

    public bool CanStartStop => _liteModeManager.CanStartStop;

    public string ControlButtonText => _liteModeManager.ControlButtonText;

    public MainViewModel(NetchAppContext appContext, LiteModeManager liteModeManager, Configuration configuration)
    {
        _appContext = appContext;
        _liteModeManager = liteModeManager;
        _configuration = configuration;
        _liteModeManager.PropertyChanged += LiteModeManager_PropertyChanged;
    }

    public void Initialize()
    {
        var settings = _appContext.Settings;
        SelectedProxyProtocol = Socks5Protocol;
        UseLocalhostOverride = settings.UseLocalhostOverride;
        ProxyHost = UseLocalhostOverride ? "127.0.0.1" : settings.LiteProxyHost;
        ProxyPort = settings.LiteProxyPort;

        _allApps = AppDiscoveryService.DiscoverInstalledApps();
        FilteredApps.Clear();
        ProcessGroups.Clear();

        var savedPaths = settings.SelectedAppPaths;
        foreach (var app in _allApps)
        {
            if (savedPaths.Contains(app.InstallPath, StringComparer.OrdinalIgnoreCase))
            {
                app.IsSelected = true;
                var group = new ProcessGroup { GroupName = app.Name };
                ScanDirectoryInto(app.InstallPath, group);
                if (group.Processes.Count > 0)
                {
                    ProcessGroups.Add(group);
                    OnPropertyChanged(nameof(SelectedAppsTitle));
                }
            }
        }

        ApplyFilter();
        OnPropertyChanged(nameof(SelectedAppsTitle));
    }

    public void Dispose()
    {
        _liteModeManager.PropertyChanged -= LiteModeManager_PropertyChanged;
        _saveCts?.Cancel();
        _saveCts?.Dispose();
    }
    public void SaveSettings()
    {
        _ = SaveSelectionAsync();
    }

    partial void OnProxyHostChanged(string value)
    {
        _ = SaveSelectionWithDebounceAsync();
    }

    partial void OnProxyPortChanged(string value)
    {
        _ = SaveSelectionWithDebounceAsync();
    }

    partial void OnUseLocalhostOverrideChanged(bool value)
    {
        if (value)
        {
            ProxyHost = "127.0.0.1";
        }

        OnPropertyChanged(nameof(IsHostEditable));
        _ = SaveSelectionWithDebounceAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void LiteModeManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LiteModeManager.CurrentState))
        {
            OnPropertyChanged(nameof(CurrentState));
            OnPropertyChanged(nameof(CanStartStop));
            OnPropertyChanged(nameof(ControlButtonText));
            StartStopCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(LiteModeManager.StatusText))
        {
            OnPropertyChanged(nameof(StatusText));
        }
        else if (e.PropertyName == nameof(LiteModeManager.CanStartStop))
        {
            OnPropertyChanged(nameof(CanStartStop));
            StartStopCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(LiteModeManager.ControlButtonText))
        {
            OnPropertyChanged(nameof(ControlButtonText));
        }
        else if (e.PropertyName == nameof(LiteModeManager.CurrentProcessNames))
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void ApplyFilter()
    {
        FilteredApps.Clear();
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var app in filtered
                     .OrderByDescending(a => a.IsSelected)
                     .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            FilteredApps.Add(app);
    }

    public void OnAppSelectionChanged(InstalledApp app)
    {
        if (app.IsSelected)
        {
            var group = new ProcessGroup { GroupName = app.Name };
            ScanDirectoryInto(app.InstallPath, group);
            if (group.Processes.Count > 0)
            {
                ProcessGroups.Add(group);
                OnPropertyChanged(nameof(SelectedAppsTitle));
            }
        }
        else
        {
            var group = ProcessGroups.FirstOrDefault(g => g.GroupName == app.Name);
            if (group != null)
            {
                ProcessGroups.Remove(group);
                OnPropertyChanged(nameof(SelectedAppsTitle));
            }
        }

        _ = SaveSelectionAsync();
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanStartStop))]
    private async Task StartStopAsync()
    {
        if (CurrentState is State.Waiting or State.Stopped)
            await StartAsync();
        else if (CurrentState == State.Started)
            await _liteModeManager.StopAsync();
    }

    private async Task StartAsync()
    {
        await SaveSelectionAsync();
        var processes = ProcessGroups.SelectMany(g => g.Processes).ToList();
        await _liteModeManager.StartAsync(ProxyHost, ProxyPort, processes);
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
        OnPropertyChanged(nameof(SelectedAppsTitle));
        var app = _allApps.FirstOrDefault(a => a.Name == group.GroupName);
        if (app != null)
            app.IsSelected = false;

        _ = SaveSelectionAsync();
        ApplyFilter();
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
            _liteModeManager.StatusText = $"访问被拒绝: {path}";
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
        settings.UseLocalhostOverride = UseLocalhostOverride;
        settings.SelectedAppPaths = _allApps
            .Where(a => a.IsSelected)
            .Select(a => a.InstallPath)
            .ToList();

        await _configuration.SaveAsync();
    }

    private async Task SaveSelectionWithDebounceAsync()
    {
        _saveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _saveCts = cts;

        try
        {
            await Task.Delay(500, cts.Token);
            await SaveSelectionAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }
}
