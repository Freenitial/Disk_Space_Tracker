using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Read-only modal for the textual disk-space report. Close button lives here for simplicity.
/// </summary>
public partial class ReportWindow : Window
{
    public ReportWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
