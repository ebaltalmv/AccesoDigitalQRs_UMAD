using UMADAccesos.ViewModels;

namespace UMADAccesos.Views;

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
