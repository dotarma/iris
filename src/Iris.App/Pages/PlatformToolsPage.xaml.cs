using Iris.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Iris.App.Pages;

public sealed partial class PlatformToolsPage : Page
{
    public PlatformToolsPage()
    {
        InitializeComponent();
        DataContext = App.Get<PlatformToolsViewModel>();
    }
}
