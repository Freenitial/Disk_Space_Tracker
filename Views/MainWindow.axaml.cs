using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Views;

/// <summary>
/// Borderless main window:
/// <list type="bullet">
/// <item>Title bar drag → <see cref="Window.BeginMoveDrag"/>.</item>
/// <item>Minimize / Close buttons in the title bar.</item>
/// <item>The Auto panel slides in at the bottom and grows the window height.</item>
/// <item>The Logs sidebar slides in at the right and grows the window width.</item>
/// </list>
/// The window is non-resizable with fixed sizes.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Base size with no Auto panel and no Logs sidebar, captured from the XAML-declared
    /// Width/Height so the base dimensions have a single source of truth (the XAML) instead of
    /// constants duplicated here.</summary>
    private double _baseWidth;
    private double _baseHeight;
    /// <summary>Height delta when the Auto panel is open: 1px separator + 255px panel.</summary>
    private const double AutoPanelExpandedHeight = 256;
    /// <summary>Width gained by the Logs sidebar when shown (matches the LogsBorder.Width).</summary>
    private const double LogsSidebarWidth = 300;

    /// <summary>The view-model we are currently subscribed to, so we can detach cleanly if the
    /// DataContext is ever reassigned (otherwise the previous VM and its Logs would strand
    /// PropertyChanged subscriptions and keep firing into this window).</summary>
    private MainWindowViewModel? _subscribedVm;

    public MainWindow()
    {
        InitializeComponent();
        // The XAML-declared Width/Height are the single source of truth for the base size.
        _baseWidth = Width;
        _baseHeight = Height;
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Detach the currently-bound view-model (and its Logs) before binding the new DataContext.
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm.Logs.PropertyChanged -= OnLogsPropertyChanged;
            _subscribedVm = null;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Subscribe to nested LogsViewModel.IsVisible changes for sidebar resize.
            vm.Logs.PropertyChanged += OnLogsPropertyChanged;
            _subscribedVm = vm;
            ApplyDimensions(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AutoPanelOpen) && DataContext is MainWindowViewModel vm)
            ApplyDimensions(vm);
    }

    private void OnLogsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogsViewModel.IsVisible) && DataContext is MainWindowViewModel vm)
            ApplyDimensions(vm);
    }

    /// <summary>Recompute and apply window dimensions from the current Auto/Logs state.</summary>
    private void ApplyDimensions(MainWindowViewModel vm)
    {
        var w = _baseWidth + (vm.Logs.IsVisible ? LogsSidebarWidth : 0);
        var h = _baseHeight + (vm.AutoPanelOpen ? AutoPanelExpandedHeight : 0);
        Width = w;
        Height = h;
        MinWidth = w;
        MinHeight = h;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnUnitLeftFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.Converter.SetActive(true);
    }

    private void OnUnitRightFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.Converter.SetActive(false);
    }
}
