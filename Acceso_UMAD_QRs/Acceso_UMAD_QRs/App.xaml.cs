using Acceso_UMAD_QRs.Views;
using SharedResources.Models;

namespace Acceso_UMAD_QRs
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var loginView = Handler?.MauiContext?.Services.GetService<LoginView>() ?? new LoginView(Handler?.MauiContext?.Services.GetService<ViewModels.UserViewModel>()!);
            return new Window(new NavigationPage(loginView));
        }
    }
}