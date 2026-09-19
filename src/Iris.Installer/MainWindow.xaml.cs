using System.Diagnostics;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.Installer;

public sealed partial class MainWindow : Window
{
    private InstallerStep _step;
    private readonly bool _isUninstall;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Iris Installer";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(980, 680));
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => UpdateTitleBar();
            UpdateTitleBar();
        }

        StepList.SelectedIndex = 0;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"Version {version}";
        _isUninstall = Environment.GetCommandLineArgs().Any(argument =>
            argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
        if (_isUninstall)
        {
            Title = "Iris Uninstaller";
            Options.InstallDirectory = InstallEngine.ResolveUninstallRoot(Options.InstallDirectory);
            WelcomePanel.Children.Insert(0, new InfoBar
            {
                IsOpen = true,
                Severity = InfoBarSeverity.Warning,
                Title = "Uninstall Iris",
                Message = "Application files will be removed. Settings, logs, and downloaded tools are preserved unless selected on the Ready step."
            });
            LocationText.IsEnabled = false;
            NextButton.Content = "Continue to uninstall";
        }

        if (!_isUninstall && !File.Exists(Path.Combine(AppContext.BaseDirectory, "payload", "Iris.App", "payload.manifest.json")))
        {
            WelcomePanel.Children.Insert(0, new InfoBar
            {
                IsOpen = true,
                Severity = InfoBarSeverity.Warning,
                Title = "Development build",
                Message = "Application payload not found next to this executable. Install requires a full package: extract artifacts/<arch>/Iris-<version>-<arch>.zip and run its Iris.Installer.exe, or run build/Package.ps1."
            });
        }
    }

    public InstallOptions Options { get; } = new();

    private static string InstallerVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private async void Next_Click(object sender, RoutedEventArgs args)
    {
        ErrorInfo.IsOpen = false;
        try
        {
            if (_isUninstall)
            {
                await RunUninstallFlowAsync().ConfigureAwait(true);
                return;
            }

            if (_step == InstallerStep.Welcome)
            {
                ShowStep(InstallerStep.SystemCheck);
                UpdateSystemCheck();
                if (!InstallEngine.Check(Options).CanInstall)
                {
                    ErrorInfo.Message = "System requirements are not satisfied. Resolve the warnings above before continuing.";
                    ErrorInfo.IsOpen = true;
                }

                return;
            }

            if (_step == InstallerStep.SystemCheck)
            {
                if (!InstallEngine.Check(Options).CanInstall)
                {
                    ErrorInfo.Message = "System requirements are not satisfied.";
                    ErrorInfo.IsOpen = true;
                    return;
                }

                ShowStep(InstallerStep.Options);
                return;
            }

            if (_step == InstallerStep.Location)
            {
                try
                {
                    _ = Path.GetFullPath(Options.InstallDirectory);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    ErrorInfo.Message = "The installation path is invalid.";
                    ErrorInfo.IsOpen = true;
                    return;
                }

                var check = InstallEngine.Check(Options);
                if (!check.DiskSpaceSufficient)
                {
                    ErrorInfo.Message = "Not enough disk space is available for the selected location.";
                    ErrorInfo.IsOpen = true;
                    return;
                }
            }

            if (_step < InstallerStep.Ready)
            {
                ShowStep(_step + 1);
                if (_step == InstallerStep.Ready)
                {
                    ReadyTitle.Text = "Ready to Install";
                    UninstallOptionsPanel.Visibility = Visibility.Collapsed;
                    ReadySummary.Text = $"Iris {InstallerVersion} ({InstallerArchitecture.Current}) will be installed to:{Environment.NewLine}{Options.InstallDirectory}";
                }

                return;
            }

            if (_step == InstallerStep.Ready)
            {
                ShowStep(InstallerStep.Installing);
                BackButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                var progress = new Progress<(double Value, string Status)>(value =>
                {
                    InstallProgress.Value = value.Value * 100;
                    ProgressStatus.Text = value.Status;
                });
                await InstallEngine.InstallAsync(Options, progress);
                CompletedTitle.Text = "Iris has been installed successfully.";
                LaunchCheck.Visibility = Visibility.Visible;
                ShowStep(InstallerStep.Completed);
                NextButton.IsEnabled = true;
                NextButton.Content = "Close";
                CancelButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (_step == InstallerStep.Completed)
            {
                if (!_isUninstall && LaunchCheck.IsChecked == true)
                {
                    var executable = Path.Combine(Options.InstallDirectory, "App", "Iris.App.exe");
                    if (File.Exists(executable))
                        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! });
                }

                Close();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or InvalidDataException
            or PlatformNotSupportedException or NotSupportedException or TimeoutException)
        {
            ErrorInfo.Message = exception.Message;
            ErrorInfo.IsOpen = true;
            if (_step == InstallerStep.Installing)
            {
                ShowStep(InstallerStep.Ready);
                NextButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            ErrorInfo.Message = "An unexpected installer error occurred.";
            ErrorInfo.IsOpen = true;
            if (_step == InstallerStep.Installing)
            {
                ShowStep(InstallerStep.Ready);
                NextButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
    }

    private async Task RunUninstallFlowAsync()
    {
        try
        {
            if (_step == InstallerStep.Welcome)
            {
                ShowStep(InstallerStep.Ready);
                ReadyTitle.Text = "Ready to Uninstall";
                UninstallOptionsPanel.Visibility = Visibility.Visible;
                ReadySummary.Text = $"The Iris installation at:{Environment.NewLine}{Options.InstallDirectory}{Environment.NewLine}will be removed. User data is preserved unless selected above.";
                NextButton.Content = "Uninstall";
                BackButton.IsEnabled = false;
                return;
            }

            if (_step == InstallerStep.Ready)
            {
                ShowStep(InstallerStep.Installing);
                BackButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                var progress = new Progress<(double Value, string Status)>(value =>
                {
                    InstallProgress.Value = value.Value * 100;
                    ProgressStatus.Text = value.Status;
                });
                await InstallEngine.UninstallAsync(Options, progress);
                CompletedTitle.Text = "Iris has been uninstalled.";
                LaunchCheck.Visibility = Visibility.Collapsed;
                ShowStep(InstallerStep.Completed);
                NextButton.IsEnabled = true;
                NextButton.Content = "Close";
                CancelButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (_step == InstallerStep.Completed)
                Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or InvalidDataException
            or PlatformNotSupportedException or NotSupportedException or TimeoutException)
        {
            ErrorInfo.Message = exception.Message;
            ErrorInfo.IsOpen = true;
            if (_step == InstallerStep.Installing)
            {
                ShowStep(InstallerStep.Ready);
                NextButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            ErrorInfo.Message = "An unexpected uninstaller error occurred.";
            ErrorInfo.IsOpen = true;
            if (_step == InstallerStep.Installing)
            {
                ShowStep(InstallerStep.Ready);
                NextButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs args)
    {
        if (_isUninstall)
            return;
        if (_step > InstallerStep.Welcome && _step < InstallerStep.Installing)
            ShowStep(_step - 1);
    }

    private void Cancel_Click(object sender, RoutedEventArgs args) => Close();

    private void UpdateSystemCheck()
    {
        var check = InstallEngine.Check(Options);
        WindowsCheck.Text = $"{(check.WindowsSupported ? "✓" : "⚠")} Windows 10 2004 or later";
        ArchitectureCheck.Text = $"{(check.ArchitectureSupported ? "✓" : "⚠")} {check.Architecture} architecture";
        DiskCheck.Text = $"{(check.DiskSpaceSufficient ? "✓" : "⚠")} At least 750 MB free";
        AdminCheck.Text = check.IsAdministrator ? "✓ Administrator privileges available" : "⚠ Administrator privileges required for Program Files";
        if (check.ExistingVersion is not null)
        {
            ExistingInfo.Message = $"Installed version: {check.ExistingVersion}. Continue to upgrade or repair.";
            ExistingInfo.IsOpen = true;
        }
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
    }

    private void ShowStep(InstallerStep step)
    {
        _step = step;
        StepList.SelectedIndex = (int)step;
        var panels = new FrameworkElement[] { WelcomePanel, CheckPanel, OptionsPanel, LocationPanel, ComponentsPanel, ReadyPanel, InstallingPanel, CompletedPanel };
        for (var index = 0; index < panels.Length; index++)
            panels[index].Visibility = index == (int)step ? Visibility.Visible : Visibility.Collapsed;
        if (_isUninstall)
        {
            BackButton.IsEnabled = false;
            NextButton.Content = step switch
            {
                InstallerStep.Ready => "Uninstall",
                InstallerStep.Completed => "Close",
                _ => "Continue"
            };
            return;
        }

        BackButton.IsEnabled = step is > InstallerStep.Welcome and < InstallerStep.Installing;
        NextButton.Content = step == InstallerStep.Ready ? "Install" : step == InstallerStep.Completed ? "Close" : "Continue";
    }
}
