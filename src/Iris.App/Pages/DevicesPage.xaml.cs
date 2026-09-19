using Iris.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        ViewModel = App.Get<DevicesViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    public DevicesViewModel ViewModel { get; }
}
