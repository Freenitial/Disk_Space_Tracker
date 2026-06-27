using Avalonia;
using Avalonia.Controls.Primitives;

namespace DiskSpaceTracker.Controls;

/// <summary>
/// Toggle switch with a horizontal line + square glyph. The visual is fully described by the
/// control template (see <c>Styles/ControlStyles.axaml</c>): a green line + gray square when
/// checked, a dark red line + gray square when unchecked.
/// </summary>
public sealed class TriggerSwitch : ToggleButton
{
    /// <summary>Text rendered to the left of the glyphs.</summary>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<TriggerSwitch, string>(nameof(Caption), string.Empty);

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TriggerSwitch);
}
