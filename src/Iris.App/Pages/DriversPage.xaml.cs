using Iris.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class DriversPage : Page
{
    public DriversPage()
    {
        InitializeComponent();
        ViewModel = App.Get<DriversViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }
    public DriversViewModel ViewModel { get; }
}
