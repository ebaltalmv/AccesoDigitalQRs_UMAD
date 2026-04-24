using Acceso_UMAD_QRs.ViewModels;
namespace Acceso_UMAD_QRs.Views;

public partial class SetupPuntoAccesoView : ContentPage
{
    public SetupPuntoAccesoView(RegistroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}