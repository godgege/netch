using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Netch.App.ViewModels;

namespace Netch.App.Views;

public sealed partial class SettingsPage : Page
{
    private const double CompactRowBreakpoint = 560;
    private const double TwoColumnBreakpoint = 900;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);
    }

    private void SettingsPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        var twoColumns = width >= TwoColumnBreakpoint;
        var compactRows = width < CompactRowBreakpoint;

        SettingsScroll.Padding = compactRows
            ? new Thickness(20, 20, 20, 28)
            : new Thickness(32, 28, 32, 36);
        SettingsLayout.MaxWidth = twoColumns ? 920 : compactRows ? 360 : 430;
        SettingsLayout.ColumnSpacing = twoColumns ? 44 : 0;
        SettingsLayout.RowSpacing = twoColumns ? 26 : 24;

        ApplySectionLayout(twoColumns);

        foreach (var row in FindSettingRows(SettingsLayout))
            ApplyRowLayout(row, compactRows);
    }

    private void ApplySectionLayout(bool twoColumns)
    {
        if (SettingsLayout.ColumnDefinitions.Count >= 2)
        {
            SettingsLayout.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            SettingsLayout.ColumnDefinitions[1].Width = twoColumns
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        }

        Grid.SetRow(HeaderSection, 0);
        Grid.SetColumn(HeaderSection, 0);
        Grid.SetColumnSpan(HeaderSection, twoColumns ? 2 : 1);

        Grid.SetRow(AppearanceSection, 1);
        Grid.SetColumn(AppearanceSection, 0);

        Grid.SetRow(ProxySection, 2);
        Grid.SetColumn(ProxySection, 0);

        Grid.SetRow(StartupSection, twoColumns ? 1 : 3);
        Grid.SetColumn(StartupSection, twoColumns ? 1 : 0);

        Grid.SetRow(UpdatesSection, twoColumns ? 2 : 4);
        Grid.SetColumn(UpdatesSection, twoColumns ? 1 : 0);

        Grid.SetRow(SaveButton, twoColumns ? 3 : 5);
        Grid.SetColumn(SaveButton, 0);
        Grid.SetColumnSpan(SaveButton, twoColumns ? 2 : 1);
    }

    private static void ApplyRowLayout(Grid row, bool compact)
    {
        if (row.ColumnDefinitions.Count >= 2)
        {
            row.ColumnDefinitions[0].Width = compact
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(132);
            row.ColumnDefinitions[1].Width = compact
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
        }

        row.ColumnSpacing = compact ? 0 : 14;
        row.RowSpacing = compact ? 6 : 0;
        row.Margin = compact ? new Thickness(16, 0, 0, 0) : new Thickness(20, 0, 0, 0);

        foreach (var child in row.Children.OfType<FrameworkElement>())
        {
            if (child is TextBlock)
            {
                Grid.SetRow(child, 0);
                Grid.SetColumn(child, 0);
                continue;
            }

            Grid.SetRow(child, compact ? 1 : 0);
            Grid.SetColumn(child, compact ? 0 : 1);
            ApplyControlLayout(child, compact);
        }
    }

    private static void ApplyControlLayout(FrameworkElement element, bool compact)
    {
        if (element.Tag is not string tag)
            return;

        element.HorizontalAlignment = tag == "SwitchInput"
            ? HorizontalAlignment.Left
            : compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        if (tag == "SwitchInput")
            return;

        if (compact)
        {
            element.ClearValue(WidthProperty);
            element.MaxWidth = tag == "LongInput" ? 280 : 220;
            return;
        }

        element.ClearValue(MaxWidthProperty);
        element.Width = tag switch
        {
            "ThemeInput" => 150,
            "NumberInput" => 160,
            "LongInput" => 220,
            _ => element.Width
        };
    }

    private static IEnumerable<Grid> FindSettingRows(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Grid { Tag: "SettingRow" } row)
                yield return row;

            foreach (var nestedRow in FindSettingRows(child))
                yield return nestedRow;
        }
    }
}