using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Modal process picker. Closes with the picked process name (without ".exe") or null on
/// cancel. The view-model is recreated by DI on every open so the list is fresh.
/// </summary>
public partial class ProcessPickerWindow : Window
{
    public ProcessPickerWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnPick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProcessPickerViewModel vm)
        {
            Close(vm.SelectedItem?.Name);
            return;
        }
        Close(null);
    }
}
