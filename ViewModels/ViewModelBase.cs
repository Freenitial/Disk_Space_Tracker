using CommunityToolkit.Mvvm.ComponentModel;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Common base for all view-models. Inherits change-notification machinery from
/// <see cref="ObservableObject"/> (CommunityToolkit.Mvvm source generators, AOT-safe).
/// </summary>
public abstract class ViewModelBase : ObservableObject;
