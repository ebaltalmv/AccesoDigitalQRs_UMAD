using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class MyQRView : ContentPage
{
    public MyQRView(AccessTokenViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccessTokenViewModel viewModel)
        {
            await viewModel.Initialize(null!);
        }
    }
}
