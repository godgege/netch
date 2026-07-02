using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Netch.App.ViewModels;

public partial class LogViewModel : ObservableObject, IDisposable
{
    private static readonly ObservableCollection<string> EmptyLogEntries = new();
    private readonly ObservableCollection<string> _allLogEntries;
    private readonly ObservableCollection<string> _defaultLogEntries;
    private readonly ObservableCollection<string> _visibleLogEntries = new();
    private bool _showAllProcessEvents;
    private bool _disposed;

    public ObservableCollection<string> VisibleLogEntries => _visibleLogEntries;

    public Visibility LogEntriesVisibility => _visibleLogEntries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyLogVisibility => _visibleLogEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public LogViewModel()
    {
        _allLogEntries = App.UiLogSink?.AllLogEntries ?? EmptyLogEntries;
        _defaultLogEntries = App.UiLogSink?.DefaultLogEntries ?? EmptyLogEntries;

        _allLogEntries.CollectionChanged += SourceLogEntries_CollectionChanged;
        _defaultLogEntries.CollectionChanged += SourceLogEntries_CollectionChanged;

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
        _allLogEntries.CollectionChanged -= SourceLogEntries_CollectionChanged;
        _defaultLogEntries.CollectionChanged -= SourceLogEntries_CollectionChanged;
    }

    private void SourceLogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ActiveSourceLogEntries()))
            RefreshVisibleLogEntries();
    }

    private void RefreshVisibleLogEntries()
    {
        if (_disposed)
            return;

        _visibleLogEntries.Clear();
        foreach (var entry in ActiveSourceLogEntries())
            _visibleLogEntries.Add(entry);

        OnPropertyChanged(nameof(LogEntriesVisibility));
        OnPropertyChanged(nameof(EmptyLogVisibility));
    }

    private ObservableCollection<string> ActiveSourceLogEntries()
    {
        return ShowAllProcessEvents ? _allLogEntries : _defaultLogEntries;
    }
}
