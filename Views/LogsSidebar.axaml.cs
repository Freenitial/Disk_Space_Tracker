using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DiskSpaceTracker.Views;

/// <summary>Logs sidebar shell — pure binding view.</summary>
public partial class LogsSidebar : UserControl
{
    public LogsSidebar()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
