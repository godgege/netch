using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Netch.App.Services;

namespace Netch.App.ViewModels;

public partial class LogViewModel : ObservableObject, IDisposable
{
    private static readonly ObservableCollection<string> EmptyLogEntries = new();
    private readonly LiteModeManager _liteModeManager;
    private readonly ObservableCollection<string> _sourceLogEntries;
    private readonly ObservableCollection<string> _visibleLogEntries = new();
    private bool _showAllProcessEvents;
    private bool _disposed;

    public ObservableCollection<string> VisibleLogEntries => _visibleLogEntries;

    public LogViewModel(LiteModeManager liteModeManager)
    {
        _liteModeManager = liteModeManager;
        _sourceLogEntries = App.UiLogSink?.LogEntries ?? EmptyLogEntries;

        _sourceLogEntries.CollectionChanged += SourceLogEntries_CollectionChanged;
        _liteModeManager.PropertyChanged += LiteModeManager_PropertyChanged;

        RefreshVisibleLogEntries();
    }

    public bool ShowAllProcessEvents
    {
        get => _showAllProcessEvents;
        set
        {
            if (SetProperty(ref _showAllProcessEvents, value))
                RefreshVisibleLogEntries();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _sourceLogEntries.CollectionChanged -= SourceLogEntries_CollectionChanged;
        _liteModeManager.PropertyChanged -= LiteModeManager_PropertyChanged;
    }

    private void SourceLogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisibleLogEntries();
    }

    private void LiteModeManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LiteModeManager.CurrentProcessNames))
            RefreshVisibleLogEntries();
    }

    private void RefreshVisibleLogEntries()
    {
        if (_disposed)
            return;

        var selectedNames = _liteModeManager.CurrentProcessNames;
        _visibleLogEntries.Clear();

        foreach (var entry in _sourceLogEntries)
        {
            if (ShouldShowEntry(entry, selectedNames))
                _visibleLogEntries.Add(entry);
        }
    }

    private bool ShouldShowEntry(string entry, IReadOnlyList<string> selectedNames)
    {
        if (!entry.Contains("[Redirector][EventHandler]", StringComparison.Ordinal))
            return true;

        if (ShowAllProcessEvents)
            return true;

        if (selectedNames.Count == 0)
            return false;

        foreach (var name in selectedNames)
        {
            if (entry.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
