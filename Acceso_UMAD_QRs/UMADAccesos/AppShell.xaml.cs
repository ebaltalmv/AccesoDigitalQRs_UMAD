using UMADAccesos.Views;

namespace UMADAccesos
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(SignUpView), typeof(SignUpView));
            Routing.RegisterRoute(nameof(ScannerView), typeof(ScannerView));
            Routing.RegisterRoute(nameof(AccessGeneratorView), typeof(AccessGeneratorView));

            BuildMenuByRole();
        }

        private void BuildMenuByRole()
        {
            this.Items.Clear();

            if (App.CurrentUser == null || App.CurrentUser.Role == null)
                return;

            var role = App.CurrentUser.Role.RoleName.ToLower();

            var myQrItem = new FlyoutItem { Title = "Mi Código QR" };
            myQrItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(MyQRView)), Route = "MyQRView" });
            this.Items.Add(myQrItem);

            if (role.Contains("guardia") || role.Contains("seguridad") || role.Contains("admin") || role.Contains("lector"))
            {
                var shiftItem = new FlyoutItem { Title = "Turno de Guardia" };
                shiftItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(AccessPointSetupView)), Route = "AccessPointSetupView" });
                this.Items.Add(shiftItem);

                var adminItem = new FlyoutItem { Title = "Administración" };
                adminItem.Items.Add(new Tab { Title = "Aprobaciones", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(ApprovalsView)), Route = "ApprovalsView" } } });
                adminItem.Items.Add(new Tab { Title = "Historial", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(HistoryView)), Route = "HistoryView" } } });
                this.Items.Add(adminItem);
            }

            var logoutItem = new MenuItem { Text = "Cerrar Sesión" };
            logoutItem.Clicked += async (s, e) =>
            {
                App.CurrentUser = null;
                var loginView = Application.Current!.Handler!.MauiContext!.Services.GetService<LoginView>();
                Application.Current.Windows[0].Page = new NavigationPage(loginView);
            };
            this.Items.Add(logoutItem);
        }
    }
}
