using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Serilog.Core;
using Serilog.Events;

namespace Netch.App.Services;

public class UiLogSink : ILogEventSink
{
    private const int MaxEntries = 1000;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<IReadOnlyList<string>> _currentProcessNames;

    public ObservableCollection<string> AllLogEntries { get; } = new();

    public ObservableCollection<string> DefaultLogEntries { get; } = new();

    public ObservableCollection<string> LogEntries => AllLogEntries;

    public UiLogSink(DispatcherQueue dispatcher, Func<IReadOnlyList<string>> currentProcessNames)
    {
        _dispatcher = dispatcher;
        _currentProcessNames = currentProcessNames;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = $"[{logEvent.Timestamp:HH:mm:ss}] {logEvent.RenderMessage()}";

        _dispatcher.TryEnqueue(() =>
        {
            AddEntry(AllLogEntries, message);

            if (ShouldShowInDefaultLog(message))
                AddEntry(DefaultLogEntries, message);
        });
    }

    private static void AddEntry(ObservableCollection<string> entries, string message)
    {
        entries.Add(message);
        while (entries.Count > MaxEntries)
            entries.RemoveAt(0);
    }

    private bool ShouldShowInDefaultLog(string entry)
    {
        if (!entry.Contains("[Redirector][EventHandler]", StringComparison.Ordinal))
            return true;

        if (IsRedirectedTrafficEntry(entry))
            return true;

        foreach (var name in _currentProcessNames())
        {
            if (entry.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsRedirectedTrafficEntry(string entry)
    {
        if (!entry.Contains("[tcpConnectRequest]", StringComparison.Ordinal) &&
            !entry.Contains("[udpCreated]", StringComparison.Ordinal))
            return false;

        return !entry.Contains("[!", StringComparison.Ordinal) &&
               !entry.Contains("[checkBypassName]", StringComparison.Ordinal);
    }
}
