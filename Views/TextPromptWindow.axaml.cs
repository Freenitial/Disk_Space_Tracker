using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>One-line text prompt modal returning the entered string or null on cancel.</summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public TextPromptWindow(string title, string label, string seed = "") : this()
    {
        Title = title;
        if (this.FindControl<TextBlock>("LabelText") is { } labelText) labelText.Text = label;
        if (this.FindControl<TextBox>("ValueBox") is { } valueBox) valueBox.Text = seed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var text = (this.FindControl<TextBox>("ValueBox")?.Text ?? string.Empty).Trim();
        Close(text.Length == 0 ? null : text);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
