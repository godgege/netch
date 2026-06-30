using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Serilog.Core;
using Serilog.Events;

namespace Netch.App.Services;

public class UiLogSink : ILogEventSink
{
    private const int MaxEntries = 1000;
    private readonly DispatcherQueue _dispatcher;

    public ObservableCollection<string> LogEntries { get; } = new();

    public UiLogSink(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = $"[{logEvent.Timestamp:HH:mm:ss}] {logEvent.RenderMessage()}";

        _dispatcher.TryEnqueue(() =>
        {
            LogEntries.Add(message);
            while (LogEntries.Count > MaxEntries)
                LogEntries.RemoveAt(0);
        });
    }
}
