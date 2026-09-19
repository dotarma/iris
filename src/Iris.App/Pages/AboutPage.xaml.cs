using Iris.App.ViewModels;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        ViewModel = App.Get<AboutViewModel>();
        DataContext = ViewModel;
    }
    public AboutViewModel ViewModel { get; }
    private async void Check_Click(object sender, RoutedEventArgs args) => await ViewModel.CheckAsync();

    private async void Download_Click(object sender, RoutedEventArgs args)
    {
        var installer = await ViewModel.PrepareAsync();
        if (installer is null)
            return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Install verified Iris update?",
            Content = "The package checksum, architecture, version, and internal file hashes were verified. Iris will now open the installer; installation remains user-controlled.",
            PrimaryButtonText = "Open installer",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        using var process = Process.Start(new ProcessStartInfo(installer)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(installer)!
        });
    }
}
