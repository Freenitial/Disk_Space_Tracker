using CommunityToolkit.Mvvm.ComponentModel;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.Services;
using DiskSpaceTracker.ViewModels.Conditions;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// One tab of the Auto panel — fully describes the conditions, after-actions, all/any logic,
/// and active toggle for a single <see cref="AutomationTriggerKind"/>. Four instances of this
/// view-model live in <see cref="AutomationPanelViewModel"/>, one per trigger.
/// </summary>
public sealed partial class AutomationTabViewModel : ViewModelBase
{
    private readonly IAutomationEngine _engine;

    public AutomationTabViewModel(
        AutomationTriggerKind kind,
        ConditionsCollectionViewModel conditions,
        AfterActionsViewModel afterActions,
        IAutomationEngine engine)
    {
        Kind = kind;
        Conditions = conditions;
        AfterActions = afterActions;
        _engine = engine;
    }

    public AutomationTriggerKind Kind { get; }

    public string Header => Kind.DisplayName();

    public ConditionsCollectionViewModel Conditions { get; }

    public AfterActionsViewModel AfterActions { get; }

    /// <summary>True ⇔ the trigger is currently armed (master switch in the Auto panel).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>True ⇔ "All conditions required" (AND); false ⇔ "Any condition required" (OR).</summary>
    [ObservableProperty]
    public partial bool AllConditions { get; set; } = true;

    partial void OnIsActiveChanged(bool value) => _engine.OnTriggerToggled(Kind);
}
