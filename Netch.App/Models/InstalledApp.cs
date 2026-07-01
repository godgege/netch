using CommunityToolkit.Mvvm.ComponentModel;

namespace Netch.App.Models;

public partial class InstalledApp : ObservableObject
{
    public required string Name { get; init; }
    public required string InstallPath { get; init; }
    public string? ExePath { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}