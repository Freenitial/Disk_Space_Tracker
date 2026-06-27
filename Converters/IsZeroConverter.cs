using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// One-way converter that returns true when the bound integer is zero. Used to show the
/// "(no presets yet)" hint when the preset list is empty.
/// </summary>
public sealed class IsZeroConverter : IValueConverter
{
    public static readonly IsZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
