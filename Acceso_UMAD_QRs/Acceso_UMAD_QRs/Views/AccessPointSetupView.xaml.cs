using Acceso_UMAD_QRs.ViewModels;
namespace Acceso_UMAD_QRs.Views;

public partial class AccessPointSetupView : ContentPage
{
    public AccessPointSetupView(AccessLogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
