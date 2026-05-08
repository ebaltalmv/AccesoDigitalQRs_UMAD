using Acceso_UMAD_QRs.ViewModels;

namespace Acceso_UMAD_QRs.Views;

public partial class HistoryView : ContentPage
{
    public HistoryView(AccessLogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccessLogViewModel viewModel)
        {
            await viewModel.Initialize(null!);
        }
    }
}
