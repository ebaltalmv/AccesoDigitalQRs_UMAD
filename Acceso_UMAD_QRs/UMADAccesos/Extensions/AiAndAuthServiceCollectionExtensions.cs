using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UMADAccesos.Services.AI;
using UMADAccesos.Services.Auth;

namespace UMADAccesos.Extensions;

/// <summary>
/// Proporciona el método de extensión para registrar el componente de IA dual, la matriz RBAC de roles y el control estricto de entorno.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public static class AiAndAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registra en el contenedor de dependencias los servicios de IA (offline y producción Azure), autorización por roles, sesión reactiva y blindaje del modo de prueba.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static IServiceCollection AddUmadAiAndAuth(
        this IServiceCollection services,
        Action<IaOptions>? configurarIa = null,
        Action<EntornoEjecucionOptions>? configurarEntorno = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        IaOptions iaOptions = new();
        configurarIa?.Invoke(iaOptions);

        EntornoEjecucionOptions entornoOptions = new();
        configurarEntorno?.Invoke(entornoOptions);

        services.AddSingleton(iaOptions);
        services.AddSingleton(entornoOptions);

        // Servicios de IA dual (Estrategia OfflineLocal + AzureAiProduction sin llaves en cliente).
        services.TryAddSingleton<IAzureAiClient>(sp =>
        {
            IaOptions opciones = sp.GetRequiredService<IaOptions>();
            ILogger<AzureAiHttpClient> logger = sp.GetRequiredService<ILogger<AzureAiHttpClient>>();
            HttpClient httpClient = new();
            return new AzureAiHttpClient(httpClient, opciones, logger);
        });

        services.AddSingleton<IIaProvider, OfflineLocalIaProvider>();
        services.AddSingleton<IIaProvider, AzureAiProductionProvider>();
        services.TryAddSingleton<IIaService, IaService>();

        // Servicios de autorización RBAC, entorno blindado y estado reactivo de sesión.
        services.TryAddSingleton<IEntornoEjecucionService, EntornoEjecucionService>();
        services.TryAddSingleton<IAutorizacionRolService, AutorizacionRolService>();
        services.TryAddSingleton<ISesionActualService, SesionActualService>();

        return services;
    }
}
