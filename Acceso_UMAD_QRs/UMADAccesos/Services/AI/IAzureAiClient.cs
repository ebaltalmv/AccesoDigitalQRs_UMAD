namespace UMADAccesos.Services.AI;

/// <summary>
/// Define el cliente tipado hacia el backend proxy HTTPS que consume Azure AI en producción.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IAzureAiClient
{
    // Indica si el endpoint backend proxy está configurado con una URL HTTPS válida.
    bool EstaConfigurado { get; }

    /// <summary>
    /// Solicita al backend proxy de Azure AI la clasificación del motivo de visita proporcionado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default);

    /// <summary>
    /// Solicita al backend proxy de Azure AI la evaluación de anomalías para el contexto de acceso indicado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAsync(ContextoEvaluacionAcceso contexto, CancellationToken ct = default);
}
