using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    public sealed class DemoApp : Application
    {
        public override void Initialize()
        {
            RequestedThemeVariant = ThemeVariant.Dark;
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = desktop.Args is { Length: 1 } && desktop.Args[0] == "tracer"
                    ? new TracerWindow() : new DemoWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
