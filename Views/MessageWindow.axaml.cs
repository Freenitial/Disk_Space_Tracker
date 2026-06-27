using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>Information modal with a single OK button.</summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message) : this()
    {
        Title = title;
        if (this.FindControl<TextBlock>("MessageText") is { } text) text.Text = message;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
