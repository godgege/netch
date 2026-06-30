using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Netch.App.ViewModels;

public partial class LogViewModel : ObservableObject
{
    public ObservableCollection<string> LogEntries { get; } = new();

    [ObservableProperty]
    private bool _autoScroll = true;

    private FileSystemWatcher? _watcher;
    private string? _logFilePath;
    private long _lastPosition;

    public void StartWatching(string logFilePath)
    {
        _logFilePath = logFilePath;
        if (!File.Exists(logFilePath)) return;

        LoadExistingEntries();

        var dir = Path.GetDirectoryName(logFilePath)!;
        var filename = Path.GetFileName(logFilePath);
        _watcher = new FileSystemWatcher(dir, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnLogFileChanged;
    }

    private void LoadExistingEntries()
    {
        if (_logFilePath == null || !File.Exists(_logFilePath)) return;

        using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        while (reader.ReadLine() is { } line)
        {
            LogEntries.Add(line);
        }
        _lastPosition = fs.Position;
    }

    private void OnLogFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_logFilePath == null) return;

        try
        {
            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            while (reader.ReadLine() is { } line)
            {
                var entry = line;
                App.MainWindow?.DispatcherQueue.TryEnqueue(() => LogEntries.Add(entry));
            }
            _lastPosition = fs.Position;
        }
        catch
        {
            // File may be locked temporarily
        }
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
