using UMADAccesos.Views;
using SharedResources.Models;

namespace UMADAccesos
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
