using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Extensions;
using ZXing.Net.Maui.Controls;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace UMADAccesos
{
    /// <summary>
    /// Configura el contenedor de dependencias, el host Blazor Hybrid, el soporte de escáner ZXing y la inicialización local.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static class MauiProgram
    {
        /// <summary>
        /// Construye y configura la instancia principal de la aplicación .NET MAUI Blazor Hybrid.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddUmadCore();
            builder.Services.AddUmadOperations();
            builder.Services.AddUmadDeviceServices();
#if MAUI_DEVFLOW
            builder.AddMauiDevFlowAgent();
#endif

            MauiApp app = builder.Build();

            IUmadDbInitializer dbInitializer = app.Services.GetRequiredService<IUmadDbInitializer>();
            Task.Run(() => dbInitializer.InitializeAsync()).GetAwaiter().GetResult();

            return app;
        }
    }
}
