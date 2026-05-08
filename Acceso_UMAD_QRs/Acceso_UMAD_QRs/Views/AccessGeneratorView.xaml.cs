using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class AccessGeneratorView : ContentPage
{
    public AccessGeneratorView(AccessTokenViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnTypeChanged(object sender, CheckedChangedEventArgs e)
    {
    }
}
