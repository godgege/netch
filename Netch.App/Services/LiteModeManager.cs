using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Netch.App.Models;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models.Modes.ProcessMode;
using Netch.Servers;
using Serilog;

namespace Netch.App.Services;

public partial class LiteModeManager : ObservableObject
{
    private readonly MainController _mainController;
    private IReadOnlyList<string> _currentProcessNames = Array.Empty<string>();

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
    }

    public async Task StartAsync(string proxyHost, string proxyPort, IReadOnlyCollection<ProcessEntry> processes)
    {
        if (!ushort.TryParse(proxyPort, out var port) || port == 0)
        {
            StatusText = "Please enter a valid port";
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

        if (processes.Count == 0)
        {
            StatusText = "No processes added";
            return;
        }

        _currentProcessNames = processes
            .Select(p => p.FileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        OnPropertyChanged(nameof(CurrentProcessNames));

        var server = new Socks5Server(proxyHost, port);
        var mode = new Redirector
        {
            FilterIntranet = true,
            FilterLoopback = false
        };

        foreach (var proc in processes)
            mode.Handle.Add(proc.FileName);

        Log.Information("Starting redirector -> {Host}:{Port}, processes: {List}",
            proxyHost, port, string.Join(", ", processes.Select(p => p.FileName)));

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

    public async Task StopAsync()
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
