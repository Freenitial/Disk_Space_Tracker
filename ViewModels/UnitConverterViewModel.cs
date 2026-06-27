using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskSpaceTracker.Helpers;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Bidirectional B/KB/MB/GB converter rendered in the top-right corner. Edits to either side
/// update the other; selecting a different unit triggers the same recompute.
/// </summary>
public sealed partial class UnitConverterViewModel : ViewModelBase
{
    public string[] Units { get; } = ["B", "KB", "MB", "GB"];

    private bool _suppress;
    private bool _activeIsLeft = true;

    [ObservableProperty]
    public partial string Left { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Right { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LeftUnit { get; set; } = "MB";

    [ObservableProperty]
    public partial string RightUnit { get; set; } = "GB";

    public void SetActive(bool isLeft) => _activeIsLeft = isLeft;

    partial void OnLeftChanged(string value)
    {
        if (_suppress || !_activeIsLeft) return;
        Recompute(fromLeft: true);
    }

    partial void OnRightChanged(string value)
    {
        if (_suppress || _activeIsLeft) return;
        Recompute(fromLeft: false);
    }

    partial void OnLeftUnitChanged(string value) => Recompute(_activeIsLeft);

    partial void OnRightUnitChanged(string value) => Recompute(_activeIsLeft);

    private void Recompute(bool fromLeft)
    {
        try
        {
            _suppress = true;
            if (fromLeft)
            {
                if (string.IsNullOrWhiteSpace(Left)) { Right = string.Empty; return; }
                var converted = UnitFormatter.Convert(Left, LeftUnit, RightUnit);
                Right = UnitFormatter.FormatConverted(converted, RightUnit);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Right)) { Left = string.Empty; return; }
                var converted = UnitFormatter.Convert(Right, RightUnit, LeftUnit);
                Left = UnitFormatter.FormatConverted(converted, LeftUnit);
            }
        }
        catch
        {
            if (fromLeft) Right = string.Empty; else Left = string.Empty;
        }
        finally
        {
            _suppress = false;
        }
    }
}
