using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// Returns the accent brush when the Auto panel is open, the regular button brush otherwise,
/// so the Auto button switches color when its drawer is expanded.
/// </summary>
public sealed class AutoButtonBackgroundConverter : IValueConverter
{
    public static readonly AutoButtonBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is bool b && b ? "DstAccent" : "DstButton";
        if (Application.Current is { } app
            && app.TryGetResource(resourceKey, ThemeVariant.Default, out var found)
            && found is IBrush brush)
        {
            return brush;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
