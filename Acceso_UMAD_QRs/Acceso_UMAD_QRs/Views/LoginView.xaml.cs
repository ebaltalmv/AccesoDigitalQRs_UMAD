using Acces_UMAD_QRs.ViewModels;

namespace Acces_UMAD_QRs.Views;

public partial class LoginView : ContentPage
{
    public LoginView(UsuarioViewModel viewModel)
    {
        InitializeComponent();

        // Conectamos la Vista con UsuarioViewModel para la autenticación
        BindingContext = viewModel;
    }
}