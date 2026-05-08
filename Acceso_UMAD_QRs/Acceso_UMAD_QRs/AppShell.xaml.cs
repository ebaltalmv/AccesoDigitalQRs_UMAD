using Acceso_UMAD_QRs.Views;

namespace Acceso_UMAD_QRs
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

            var myQrItem = new FlyoutItem { Title = "My QR Code" };
            myQrItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(MyQRView)), Route = "MyQRView" });
            this.Items.Add(myQrItem);

            if (role.Contains("guardia") || role.Contains("seguridad") || role.Contains("admin") || role.Contains("lector"))
            {
                var shiftItem = new FlyoutItem { Title = "Guard Shift" };
                shiftItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(AccessPointSetupView)), Route = "AccessPointSetupView" });
                this.Items.Add(shiftItem);

                var adminItem = new FlyoutItem { Title = "Administration" };
                adminItem.Items.Add(new Tab { Title = "Approvals", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(ApprovalsView)), Route = "ApprovalsView" } } });
                adminItem.Items.Add(new Tab { Title = "History", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(HistoryView)), Route = "HistoryView" } } });
                this.Items.Add(adminItem);
            }

            var logoutItem = new MenuItem { Text = "Log Out" };
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
