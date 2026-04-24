using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class GeneradorAccesoView : ContentPage
{
    public GeneradorAccesoView(TokenAccesoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnTypeChanged(object sender, CheckedChangedEventArgs e)
    {
    }
}