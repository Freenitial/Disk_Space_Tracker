using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Controls;

/// <summary>
/// Code-behind for the preset menu button. The Apply / Delete buttons inside the popup pass
/// the bound preset name through their <c>Tag</c> property; this class forwards the click to
/// the corresponding command on <see cref="PresetSelectorViewModel"/>. Using code-behind here
/// avoids the fragile compiled-binding cast chain to the parent UserControl's DataContext.
/// </summary>
public partial class PresetMenuButton : UserControl
{
    public PresetMenuButton()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is PresetSelectorViewModel vm)
            vm.ApplyCommand.Execute(name);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is PresetSelectorViewModel vm)
            vm.DeleteCommand.Execute(name);
    }
}
