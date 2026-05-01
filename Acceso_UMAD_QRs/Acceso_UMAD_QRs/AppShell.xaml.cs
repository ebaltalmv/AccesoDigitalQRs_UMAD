using Acceso_UMAD_QRs.Views;

namespace Acceso_UMAD_QRs
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(RegistroView), typeof(RegistroView));
            Routing.RegisterRoute(nameof(EscanerView), typeof(EscanerView));
            Routing.RegisterRoute(nameof(GeneradorAccesoView), typeof(GeneradorAccesoView));

            ConstruirMenuPorRol();
        }

        private void ConstruirMenuPorRol()
        {
            this.Items.Clear();

            if (App.UsuarioActual == null || App.UsuarioActual.Rol == null)
                return;

            var rol = App.UsuarioActual.Rol.NombreRol.ToLower();

            var miQrItem = new FlyoutItem { Title = "Mi Código QR" };
            miQrItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(MiQRView)), Route = "MiQRView" });
            this.Items.Add(miQrItem);

            if (rol.Contains("guardia") || rol.Contains("seguridad") || rol.Contains("admin") || rol.Contains("lector"))
            {
                var turnoItem = new FlyoutItem { Title = "Turno de Guardia" };
                turnoItem.Items.Add(new ShellContent { ContentTemplate = new DataTemplate(typeof(SetupPuntoAccesoView)), Route = "SetupPuntoAccesoView" });
                this.Items.Add(turnoItem);

                var administracionItem = new FlyoutItem { Title = "Administración" };
                administracionItem.Items.Add(new Tab { Title = "Aprobaciones", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(AprobacionesView)), Route = "AprobacionesView" } } });
                administracionItem.Items.Add(new Tab { Title = "Historial", Items = { new ShellContent { ContentTemplate = new DataTemplate(typeof(HistorialView)), Route = "HistorialView" } } });
                this.Items.Add(administracionItem);
            }

            var logoutItem = new MenuItem { Text = "Cerrar Sesión" };
            logoutItem.Clicked += async (s, e) =>
            {
                App.UsuarioActual = null;
                var loginView = Application.Current!.Handler!.MauiContext!.Services.GetService<LoginView>();
                Application.Current.Windows[0].Page = new NavigationPage(loginView);
            };
            this.Items.Add(logoutItem);
        }
    }
}
