using Microsoft.Extensions.Logging;

namespace UMADAccesos.Services.AI;

/// <summary>
/// Proveedor de IA de producción que delega clasificación y detección de anomalías al backend proxy de Azure AI.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class AzureAiProductionProvider : IIaProvider
{
    // Cliente tipado HTTPS hacia el backend proxy de Azure AI.
    private readonly IAzureAiClient _azureAiClient;

    // Registrador operativo de invocaciones remotas de IA.
    private readonly ILogger<AzureAiProductionProvider> _logger;

    // Modo de ejecución de producción en Azure AI.
    public ModoProveedorIa Modo => ModoProveedorIa.AzureAiProduction;

    // Nombre lógico del proveedor de producción.
    public string NombreProveedor => "AzureAiProduction";

    /// <summary>
    /// Inicializa una nueva instancia del proveedor de producción respaldado por el cliente HTTPS de Azure AI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public AzureAiProductionProvider(
        IAzureAiClient azureAiClient,
        ILogger<AzureAiProductionProvider> logger)
    {
        _azureAiClient = azureAiClient ?? throw new ArgumentNullException(nameof(azureAiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Delega la clasificación del motivo de visita al backend proxy HTTPS de Azure AI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default)
    {
        if (!_azureAiClient.EstaConfigurado)
        {
            throw new InvalidOperationException("El cliente Azure AI no cuenta con un endpoint proxy HTTPS configurado.");
        }

        _logger.LogDebug("Delegando clasificación de motivo de visita al proveedor {Proveedor}.", NombreProveedor);
        return await _azureAiClient.ClasificarMotivoVisitaAsync(textoLibre, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delega la evaluación de anomalías de acceso al backend proxy HTTPS de Azure AI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAsync(ContextoEvaluacionAcceso contexto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (!_azureAiClient.EstaConfigurado)
        {
            throw new InvalidOperationException("El cliente Azure AI no cuenta con un endpoint proxy HTTPS configurado.");
        }

        _logger.LogDebug(
            "Delegando evaluación de anomalías para PersonaId {PersonaId} al proveedor {Proveedor}.",
            contexto.PersonaId,
            NombreProveedor);
        return await _azureAiClient.EvaluarAnomaliaAsync(contexto, ct).ConfigureAwait(false);
    }
}
