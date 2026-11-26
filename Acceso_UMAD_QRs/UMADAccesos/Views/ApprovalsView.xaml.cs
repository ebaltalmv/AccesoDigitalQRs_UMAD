using UMADAccesos.ViewModels;

namespace UMADAccesos.Views;

public partial class ApprovalsView : ContentPage
{
    public ApprovalsView(AccessTokenViewModel viewModel)
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
