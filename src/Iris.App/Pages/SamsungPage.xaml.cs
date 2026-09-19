using Iris.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class SamsungPage : Page
{
    public SamsungPage()
    {
        InitializeComponent();
        ViewModel = App.Get<SamsungViewModel>();
        DataContext = ViewModel;
    }

    public SamsungViewModel ViewModel { get; }

    private async void List_Click(object sender, RoutedEventArgs args) => await ViewModel.ListAsync();

    private async void RefreshTargets_Click(object sender, RoutedEventArgs args) => await ViewModel.RefreshTargetsAsync();

    private async void RebootDownload_Click(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Reboot Samsung device into Download Mode?",
            Content = "brokkR sends a reboot command over the Samsung serial port. Keep the screen unlocked and Auto Blocker off. If every device is already in Odin mode, brokkR reports 'No files selected', which means nothing needed rebooting.",
            PrimaryButtonText = "Reboot into Download Mode",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.RebootDownloadAsync();
    }

    private async void Flash_Click(object sender, RoutedEventArgs args)
    {
        var selected = new (string Slot, string Path)[]
        {
            ("BL", ViewModel.BlPath), ("AP", ViewModel.ApPath), ("CP", ViewModel.CpPath),
            ("CSC", ViewModel.CscPath), ("USERDATA", ViewModel.UserDataPath), ("PIT", ViewModel.PitPath)
        }.Where(slot => !string.IsNullOrWhiteSpace(slot.Path)).ToArray();
        if (string.IsNullOrWhiteSpace(ViewModel.Target) || selected.Length == 0)
        {
            await ShowAsync("Target and firmware required", "Refresh targets and enter one exact target, then provide at least one of BL/AP/CP/CSC/USERDATA/PIT.");
            return;
        }

        var warning = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Flash {ViewModel.Target}?",
            Content = $"Operation: flash {string.Join(", ", selected.Select(slot => slot.Slot))} to target '{ViewModel.Target}'. This can erase data, prevent startup, or permanently damage an incompatible device. brokkR checks MD5/XXH3 integrity but cannot verify the firmware matches your exact model. Keep USB and power connected throughout.",
            PrimaryButtonText = "Flash",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await warning.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.FlashAsync();
    }

    private async Task ShowAsync(string title, string content) => await new ContentDialog
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = content,
        CloseButtonText = "Close"
    }.ShowAsync();
}
