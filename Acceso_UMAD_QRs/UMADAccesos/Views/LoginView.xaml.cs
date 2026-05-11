using UMADAccesos.ViewModels;

namespace UMADAccesos.Views;

public partial class LoginView : ContentPage
{
    public LoginView(UserViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
