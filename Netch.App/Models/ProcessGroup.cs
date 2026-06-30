using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Netch.App.Models;

public partial class ProcessGroup : ObservableObject
{
    public string GroupName { get; init; } = "";

    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    [ObservableProperty]
    private string _displayText = "";

    public ProcessGroup()
    {
        Processes.CollectionChanged += OnProcessesChanged;
    }

    private void OnProcessesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        DisplayText = string.Join(", ", Processes.Select(p => p.FileName));
    }
}
