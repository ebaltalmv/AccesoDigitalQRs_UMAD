using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class RegistroView : ContentPage
{
    private readonly UsuarioViewModel _viewModel;

    public RegistroView(UsuarioViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.GetRolesAsync();
    }
}