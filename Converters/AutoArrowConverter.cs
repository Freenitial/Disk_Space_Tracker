using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// Returns "▴" when the Auto panel is open and "▿" when it is closed. Used by the Auto button
/// label so the arrow direction tracks the panel's expand/collapse state.
/// </summary>
public sealed class AutoArrowConverter : IValueConverter
{
    public static readonly AutoArrowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool open && open ? "▴" : "▿";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
