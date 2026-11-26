using UMADAccesos.ViewModels;

namespace UMADAccesos.Views;

public partial class AccessGeneratorView : ContentPage
{
    public AccessGeneratorView(AccessTokenViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnTypeChanged(object? sender, CheckedChangedEventArgs e)
    {
    }
}
