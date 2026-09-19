using Iris.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Get<SettingsViewModel>();
        DataContext = ViewModel;
    }
    public SettingsViewModel ViewModel { get; }
    private async void Save_Click(object sender, RoutedEventArgs args)
    {
        await ViewModel.SaveAsync();
        if (!ViewModel.HasError)
            App.RefreshTheme();
        SavedInfo.IsOpen = !ViewModel.HasError;
    }
}
