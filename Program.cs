using System;
using Avalonia;

namespace DiskSpaceTracker;

/// <summary>
/// Process entry point. <see cref="BuildAvaloniaApp"/> stays explicit so the AOT compiler
/// can keep all referenced controls / app types reachable through the static graph.
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia builder. Invoked both by <see cref="Main"/> and by the previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Software renderer: zero GPU dependency (no opengl32/ANGLE/Vulkan loaded at
            // runtime), and lets the linker dead-code-strip GL/Vulkan symbols from the
            // statically-linked Skia. Fine for this UI — no heavy animation.
            .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
            .WithInterFont()
            .LogToTrace();
}
