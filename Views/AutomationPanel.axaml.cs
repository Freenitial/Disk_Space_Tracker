using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Bottom panel hosting the four trigger tabs and the presets selector. Pure shell.
/// </summary>
public partial class AutomationPanel : UserControl
{
    public AutomationPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
