using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Reusable content for one trigger tab. Bound to <see cref="ViewModels.AutomationTabViewModel"/>.
/// All four tabs share this template — no behavior in code-behind.
/// </summary>
public partial class AutomationTabContent : UserControl
{
    public AutomationTabContent()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
