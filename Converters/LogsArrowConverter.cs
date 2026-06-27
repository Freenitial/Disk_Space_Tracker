using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// "Logs ◂" when the sidebar is shown, "Logs ▹" when it is hidden, so the arrow direction
/// tracks the sidebar state.
/// </summary>
public sealed class LogsArrowConverter : IValueConverter
{
    public static readonly LogsArrowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool open && open ? "Logs ◂" : "Logs ▹";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
