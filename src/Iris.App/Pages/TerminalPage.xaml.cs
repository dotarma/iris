using Iris.App.ViewModels;
using Iris.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Iris.App.Pages;

public sealed partial class TerminalPage : Page
{
    public TerminalPage()
    {
        InitializeComponent();
        ViewModel = App.Get<TerminalViewModel>();
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    public TerminalViewModel ViewModel { get; }

    private async void Run_Click(object sender, RoutedEventArgs args)
    {
        CommandInvocation invocation;
        try { invocation = ViewModel.Parse(); }
        catch (FormatException exception)
        {
            await ShowMessageAsync("Command rejected", exception.Message);
            return;
        }

        if (invocation.ChangesDeviceState)
        {
            var target = App.Get<IDeviceSelectionService>().SelectedDevice;
            var targetText = target is null
                ? "No device is currently selected."
                : $"{target.DisplayName} | Serial: {target.Serial} | Transport: {target.Transport} | State: {target.State}";
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Run a device-changing command?",
                Content = $"Command: {invocation.Executable} {string.Join(' ', invocation.Arguments)}{Environment.NewLine}{Environment.NewLine}Target: {targetText}{Environment.NewLine}{Environment.NewLine}This is not a read-only query. Verify the exact command and target before continuing.",
                PrimaryButtonText = "Run command",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        await ViewModel.ExecuteAsync(invocation);
    }

    private void Command_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            Run_Click(sender, new RoutedEventArgs());
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.Output);
        Clipboard.SetContent(package);
    }

    private async Task ShowMessageAsync(string title, string message) => await new ContentDialog
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = message,
        CloseButtonText = "Close"
    }.ShowAsync();
}
