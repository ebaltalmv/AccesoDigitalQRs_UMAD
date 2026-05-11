using UMADAccesos.ViewModels;
namespace UMADAccesos.Views;

public partial class AccessPointSetupView : ContentPage
{
    public AccessPointSetupView(AccessLogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
