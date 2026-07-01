using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Netch.App.Models;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models.Modes.ProcessMode;
using Netch.Servers;
using Netch.Utils;
using Serilog;

namespace Netch.App.Services;

public partial class LiteModeManager : ObservableObject
{
    private const ulong TrafficLogStepBytes = 1024 * 1024;

    private readonly MainController _mainController;
    private readonly DispatcherQueue _dispatcherQueue;
    private IReadOnlyList<string> _currentProcessNames = Array.Empty<string>();
    private CancellationTokenSource? _trafficMonitorCts;
    private Task? _trafficMonitorTask;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartStop))]
    [NotifyPropertyChangedFor(nameof(ControlButtonText))]
    private State _currentState = State.Waiting;

    [ObservableProperty]
    private string _statusText = "Waiting for command";

    public bool CanStartStop => CurrentState is State.Waiting or State.Stopped or State.Started;

    public string ControlButtonText => CurrentState switch
    {
        State.Waiting or State.Stopped => "Start",
        State.Started => "Stop",
        State.Starting => "Starting...",
        State.Stopping => "Stopping...",
        _ => string.Empty
    };

    public IReadOnlyList<string> CurrentProcessNames => _currentProcessNames;

    public LiteModeManager(MainController mainController)
    {
        _mainController = mainController;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task StartAsync(string proxyHost, string proxyPort, IReadOnlyCollection<ProcessEntry> processes)
    {
        if (!ushort.TryParse(proxyPort, out var port) || port == 0)
        {
            StatusText = "Please enter a valid port";
            return;
        }

        var selectedProcesses = processes
            .Where(p => p.IsSelected)
            .Where(p => !string.IsNullOrWhiteSpace(p.FileName))
            .GroupBy(p => p.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();

        if (selectedProcesses.Length == 0)
        {
            StatusText = "No processes added";
            return;
        }

        StatusText = "Testing proxy connectivity...";
        var ip = await ResolveHostAsync(proxyHost);
        if (ip == null)
        {
            Log.Warning("Cannot resolve host {Host}", proxyHost);
            StatusText = $"Cannot resolve host: {proxyHost}";
            return;
        }

        var delay = await Netch.Utils.Utils.TCPingAsync(ip, port, 2000);
        if (delay >= 2000)
        {
            Log.Warning("Proxy {Host}:{Port} is not reachable (timeout)", proxyHost, port);
            StatusText = $"Proxy not reachable: {proxyHost}:{port} (timeout)";
            return;
        }

        Log.Information("Proxy {Host}:{Port} connected, delay {Delay}ms", proxyHost, port, delay);

        _currentProcessNames = selectedProcesses
            .Select(p => p.FileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        OnPropertyChanged(nameof(CurrentProcessNames));

        var server = new Socks5Server(proxyHost, port);
        var mode = new Redirector
        {
            FilterIntranet = true,
            FilterLoopback = false,
            HandleOnlyDNS = false
        };

        var handleRules = selectedProcesses
            .SelectMany(CreateHandleRules)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var rule in handleRules)
            mode.Handle.Add(rule);

        Log.Information("Selected process paths ({Count}): {List}",
            selectedProcesses.Length, string.Join("; ", selectedProcesses.Select(p => p.FullPath)));
        Log.Information("Redirector handle rules ({Count}): {Rules}",
            handleRules.Length, string.Join("; ", handleRules));
        Log.Information("Starting redirector -> {Host}:{Port}, processes: {List}",
            proxyHost, port, string.Join(", ", selectedProcesses.Select(p => p.FileName)));

        try
        {
            CurrentState = State.Starting;
            StatusText = "Starting...";
            await _mainController.StartAsync(server, mode);
            CurrentState = State.Started;
            StatusText = "Running - no redirected traffic yet";
            StartTrafficMonitor();
            Log.Information("Redirector started successfully");
        }
        catch (Exception ex)
        {
            await StopTrafficMonitorAsync();
            _currentProcessNames = Array.Empty<string>();
            OnPropertyChanged(nameof(CurrentProcessNames));
            Log.Error(ex, "Redirector start failed");
            StatusText = ex.Message;
            CurrentState = State.Stopped;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            CurrentState = State.Stopping;
            StatusText = "Stopping...";
            await StopTrafficMonitorAsync();
            await _mainController.StopAsync();
            _currentProcessNames = Array.Empty<string>();
            OnPropertyChanged(nameof(CurrentProcessNames));
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

    private static IEnumerable<string> CreateHandleRules(ProcessEntry process)
    {
        var fullPath = process.FullPath.Trim();
        if (!string.IsNullOrWhiteSpace(fullPath))
            yield return "^" + fullPath.ToRegexString() + "$";

        var fileName = process.FileName.Trim();
        if (!string.IsNullOrWhiteSpace(fileName))
            yield return fileName.ToRegexString();
    }

    private void StartTrafficMonitor()
    {
        var cts = new CancellationTokenSource();
        _trafficMonitorCts = cts;
        _trafficMonitorTask = MonitorTrafficAsync(cts.Token);
    }

    private async Task StopTrafficMonitorAsync()
    {
        var cts = _trafficMonitorCts;
        var task = _trafficMonitorTask;
        if (cts == null)
            return;

        cts.Cancel();
        try
        {
            if (task != null)
                await task;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(cts, _trafficMonitorCts))
            {
                _trafficMonitorCts = null;
                _trafficMonitorTask = null;
            }
        }
    }

    private async Task MonitorTrafficAsync(CancellationToken token)
    {
        ulong lastLoggedTotal = 0;

        while (true)
        {
            await Task.Delay(1000, token);

            var upload = Netch.Interops.Redirector.GetUploadBytes();
            var download = Netch.Interops.Redirector.GetDownloadBytes();
            var total = upload + download;

            UpdateStatusText(total == 0
                ? "Running - no redirected traffic yet"
                : $"Running - UP {FormatBytes(upload)} / DOWN {FormatBytes(download)}");

            if (total > 0 && (lastLoggedTotal == 0 || total - lastLoggedTotal >= TrafficLogStepBytes))
            {
                Log.Information("Redirector traffic: UP {Upload}, DOWN {Download}",
                    FormatBytes(upload), FormatBytes(download));
                lastLoggedTotal = total;
            }
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] suffixes = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        var value = (double)bytes;
        var index = 0;

        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {suffixes[index]}";
    }
    private void UpdateStatusText(string value)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            StatusText = value;
            return;
        }

        _dispatcherQueue.TryEnqueue(() => StatusText = value);
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