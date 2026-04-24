using Acces_UMAD_QRs.ViewModels;

namespace Acces_UMAD_QRs.Views;

public partial class HistorialView : ContentPage
{
    public HistorialView(RegistroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RegistroViewModel viewModel)
        {
            await viewModel.Initialize(null!);
        }
    }
}