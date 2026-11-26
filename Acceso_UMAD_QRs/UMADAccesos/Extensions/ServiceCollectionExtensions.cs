using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Storage;
using UMADAccesos.Data;
using UMADAccesos.Services.Security;
using UMADAccesos.Services.Sync;

namespace UMADAccesos.Extensions
{
    /// <summary>
    /// Proporciona métodos de extensión para registrar el núcleo de persistencia, seguridad y sincronización de UMAD Accesos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static class ServiceCollectionExtensions
    {
        // Nombre del archivo SQLite local por defecto.
        private const string NombreBaseDatosPorDefecto = "UmadLocal.db";

        /// <summary>
        /// Registra en el contenedor de dependencias el DbContextFactory SQLite, inicializador, servicios de seguridad y cola offline.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public static IServiceCollection AddUmadCore(this IServiceCollection services, string? databasePath = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            string rutaBaseDatos = string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine(FileSystem.AppDataDirectory, NombreBaseDatosPorDefecto)
                : databasePath;

            services.AddDbContextFactory<UmadDbContext>(options =>
                options.UseSqlite($"Data Source={rutaBaseDatos}"));

            services.AddSingleton<IUmadDbInitializer, UmadDbInitializer>();
            services.TryAddSingleton<ISecureStorage>(_ => SecureStorage.Default);
            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddSingleton<ITokenKeyProvider, SecureStorageTokenKeyProvider>();
            services.AddScoped<ITokenAccesoService, TokenAccesoService>();
            services.AddScoped<ISyncQueueService, SyncQueueService>();

            return services;
        }
    }
}
