using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Code-behind only forwards focus events so the view-model can decide which side is the
/// "active" input — the editing direction depends on whether the user is typing in the
/// left or right text box.
/// </summary>
public partial class UnitConverterControl : UserControl
{
    public UnitConverterControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnLeftFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UnitConverterViewModel vm) vm.SetActive(true);
    }

    private void OnRightFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UnitConverterViewModel vm) vm.SetActive(false);
    }
}
