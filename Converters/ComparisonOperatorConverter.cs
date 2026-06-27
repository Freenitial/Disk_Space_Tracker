using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Converters;

/// <summary>
/// Two-way converter for radio buttons bound to a <see cref="ComparisonOperator"/>: returns
/// true when the bound enum value matches the converter parameter, and emits the matching
/// enum value back when the radio is checked.
/// </summary>
public sealed class ComparisonOperatorConverter : IValueConverter
{
    public static readonly ComparisonOperatorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ComparisonOperator op) return false;
        if (parameter is not string token) return false;
        return op == ResolveToken(token);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || !b) return Avalonia.Data.BindingOperations.DoNothing;
        if (parameter is not string token) return Avalonia.Data.BindingOperations.DoNothing;
        return ResolveToken(token);
    }

    // Tokens are the enum member names (set as XAML ConverterParameter); parse straight to the enum
    // so the mapping can't drift as operators are added. Unknown tokens fall back to Equal.
    private static ComparisonOperator ResolveToken(string token)
        => Enum.TryParse<ComparisonOperator>(token, ignoreCase: false, out var op) ? op : ComparisonOperator.Equal;
}
