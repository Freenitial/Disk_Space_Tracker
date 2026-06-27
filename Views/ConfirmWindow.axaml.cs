using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>Yes/No confirmation modal returning a <see cref="bool"/>.</summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public ConfirmWindow(string title, string message) : this()
    {
        Title = title;
        if (this.FindControl<TextBlock>("MessageText") is { } text) text.Text = message;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);
    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
