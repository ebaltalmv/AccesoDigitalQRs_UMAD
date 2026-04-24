using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class AprobacionesView : ContentPage
{
    public AprobacionesView(TokenAccesoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TokenAccesoViewModel viewModel)
        {
            await viewModel.Initialize(null!);
        }
    }
}