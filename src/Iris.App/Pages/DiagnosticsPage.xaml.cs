using Iris.App.ViewModels;
using Iris.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Iris.App.Pages;

public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        ViewModel = App.Get<DiagnosticsViewModel>();
        DataContext = ViewModel;
    }

    public DiagnosticsViewModel ViewModel { get; }

    private async void Refresh_Click(object sender, RoutedEventArgs args) => await ViewModel.RefreshAsync();

    private void Copy_Click(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.DiagnosticsText);
        Clipboard.SetContent(package);
    }

    private async void Export_Click(object sender, RoutedEventArgs args)
    {
        var path = await PickPathAsync("diagnostics.txt");
        if (path is not null)
        {
            var report = await App.Get<IDiagnosticsService>().CreateAsync();
            await App.Get<IDiagnosticsService>().ExportAsync(report, path);
        }
    }

    private async void ExportLogs_Click(object sender, RoutedEventArgs args)
    {
        var path = await PickPathAsync("iris-logs.jsonl");
        if (path is not null)
            await App.Get<ILogService>().ExportAsync(path);
    }

    private static async Task<string?> PickPathAsync(string suggestedName)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add("Text file", [Path.GetExtension(suggestedName)]);
        var window = ((App)Microsoft.UI.Xaml.Application.Current).RootWindow;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        return (await picker.PickSaveFileAsync())?.Path;
    }
}
