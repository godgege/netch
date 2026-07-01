using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class LogPage : Page
{
    private const double BottomTolerance = 4;
    private bool _isPinnedToBottom = true;

    public LogViewModel ViewModel { get; }

    public LogPage()
    {
        ViewModel = App.Services.GetRequiredService<LogViewModel>();
        InitializeComponent();

        ViewModel.VisibleLogEntries.CollectionChanged += LogEntries_CollectionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Unloaded += LogPage_Unloaded;

        ScrollToBottom();
    }

    private void LogPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.VisibleLogEntries.CollectionChanged -= LogEntries_CollectionChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        LogScrollViewer.ViewChanged -= LogScrollViewer_ViewChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ShowAllProcessEvents))
            ScrollToBottom();
    }

    private void LogEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isPinnedToBottom)
            ScrollToBottom();
    }

    private void LogScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        _isPinnedToBottom = IsAtBottom();
    }

    private void ScrollToBottom()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LogScrollViewer.UpdateLayout();
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null, true);
            _isPinnedToBottom = true;
        });
    }

    private bool IsAtBottom()
    {
        return LogScrollViewer.ScrollableHeight - LogScrollViewer.VerticalOffset <= BottomTolerance;
    }
}
