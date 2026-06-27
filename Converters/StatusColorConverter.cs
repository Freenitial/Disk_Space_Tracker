using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// Maps a <see cref="RunState"/> to the title-bar status color: green when running, yellow
/// when paused, gray when reset.
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    // Cached immutable brushes — the converter is hit on every RunState change (and re-evaluation),
    // so we hand back shared instances instead of allocating a new SolidColorBrush on each call.
    private static readonly IBrush StartedBrush = new ImmutableSolidColorBrush(Color.FromRgb(46, 204, 113));
    private static readonly IBrush PausedBrush = new ImmutableSolidColorBrush(Color.FromRgb(241, 196, 15));
    private static readonly IBrush ResetBrush = new ImmutableSolidColorBrush(Color.FromRgb(190, 190, 190));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RunState state) return Brushes.Gray;
        return state switch
        {
            RunState.Started => StartedBrush,
            RunState.Paused => PausedBrush,
            _ => ResetBrush,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
