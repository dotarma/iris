using Iris.App.Pages;
using Iris.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Iris - Android Device Toolkit";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 760));
        App.RefreshTheme();
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => UpdateTitleBar();
            UpdateTitleBar();
        }

        Navigation.SelectedItem = Navigation.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));
        Closed += (_, _) => App.Get<ILogService>().Write("Information", "app.stop", "Iris stopped.");
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string tag)
            return;
        var page = tag switch
        {
            "home" => typeof(HomePage),
            "devices" => typeof(DevicesPage),
            "terminal" => typeof(TerminalPage),
            "tools" => typeof(PlatformToolsPage),
            "drivers" => typeof(DriversPage),
            "samsung" => typeof(SamsungPage),
            "diagnostics" => typeof(DiagnosticsPage),
            "about" => typeof(AboutPage),
            _ => typeof(HomePage)
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    private void UpdateTitleBar()
    {
        var dark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
        var bar = AppWindow.TitleBar;
        var foreground = dark ? Colors.White : Colors.Black;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = foreground;
        bar.ButtonHoverForegroundColor = foreground;
        bar.ButtonPressedForegroundColor = foreground;
        bar.ButtonInactiveForegroundColor = dark
            ? Windows.UI.Color.FromArgb(255, 160, 160, 160)
            : Windows.UI.Color.FromArgb(255, 120, 120, 120);
        bar.ButtonHoverBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(36, 255, 255, 255)
            : Windows.UI.Color.FromArgb(24, 0, 0, 0);
        bar.ButtonPressedBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(56, 255, 255, 255)
            : Windows.UI.Color.FromArgb(36, 0, 0, 0);
    }
}
