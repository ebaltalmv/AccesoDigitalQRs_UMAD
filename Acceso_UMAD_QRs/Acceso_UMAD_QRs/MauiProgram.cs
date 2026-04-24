using Acceso_UMAD_QRs.ViewModels;
using Acceso_UMAD_QRs.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedResources.Data;
using ZXing.Net.Maui.Controls;

namespace Acceso_UMAD_QRs
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "UmadLocal.db");

            builder.Services.AddDbContext<UmadDbContext>(options =>
                options.UseSqlite($"Filename={dbPath}"));

            builder.Services.AddTransient<LoginView>();
            builder.Services.AddTransient<RegistroView>();
            builder.Services.AddTransient<MiQRView>();
            builder.Services.AddTransient<SetupPuntoAccesoView>();
            builder.Services.AddTransient<EscanerView>();
            builder.Services.AddTransient<AprobacionesView>();
            builder.Services.AddTransient<GeneradorAccesoView>();
            builder.Services.AddTransient<HistorialView>();

            builder.Services.AddTransient<UsuarioViewModel>();
            builder.Services.AddTransient<RolViewModel>();
            builder.Services.AddTransient<RegistroViewModel>();
            builder.Services.AddTransient<TokenAccesoViewModel>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<UmadDbContext>();
                dbContext.Database.EnsureCreated();
            }

            return app;
        }
    }
}
