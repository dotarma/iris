using Iris.App.ViewModels;
using Iris.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class HomePage : Page
{
    private bool _confirming;

    public HomePage()
    {
        InitializeComponent();
        ViewModel = App.Get<HomeViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    public HomeViewModel ViewModel { get; }

    private async void Reboot_Click(object sender, RoutedEventArgs args)
    {
        if (_confirming)
            return;
        var target = ViewModel.SelectedDevice;
        if (sender is not FrameworkElement { Tag: string mode } || target is null)
            return;
        _confirming = true;
        try
        {
            var label = mode == "normal" ? "system" : mode;
            App.Get<ILogService>().Write("Information", "device.reboot-prompt",
                $"Reboot prompt for {target.Serial} ({target.Transport}) to {label}.");
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = $"Reboot {target.DisplayName}?",
                Content = $"Serial: {target.Serial} | Transport: {target.Transport} | State: {target.State}{Environment.NewLine}{Environment.NewLine}The selected device will leave its current state and reboot to {label}. Confirm that this is the intended target.",
                PrimaryButtonText = "Reboot",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                App.Get<ILogService>().Write("Information", "device.reboot-confirmed",
                    $"Reboot confirmed for {target.Serial} ({target.Transport}) to {label}.");
                await ViewModel.RebootTargetAsync(target, mode == "normal" ? null : mode);
            }
            else
            {
                App.Get<ILogService>().Write("Information", "device.reboot-cancelled",
                    $"Reboot cancelled for {target.Serial}.");
            }
        }
        finally
        {
            _confirming = false;
        }
    }
}
