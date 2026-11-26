namespace UMADAccesos
{
    /// <summary>
    /// Punto de entrada visual de la aplicación .NET MAUI Blazor Hybrid sin estado global estático ni Service Locator.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public partial class App : Application
    {
        /// <summary>
        /// Inicializa los recursos globales de la aplicación MAUI.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Crea la ventana principal de la aplicación montando la página contenedora del BlazorWebView.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage())
            {
                Title = "UMAD Accesos v2.0"
            };
        }
    }
}
