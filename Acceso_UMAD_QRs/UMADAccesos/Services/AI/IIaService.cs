namespace UMADAccesos.Services.AI;

/// <summary>
/// Expone el servicio fachada de inteligencia artificial para clasificación de visitas y evaluación de anomalías.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IIaService
{
    // Modo de proveedor de IA configurado actualmente en la aplicación.
    ModoProveedorIa ModoActivo { get; }

    /// <summary>
    /// Clasifica un motivo de visita en lenguaje natural utilizando el proveedor activo o su respaldo offline.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default);

    /// <summary>
    /// Evalúa anomalías de acceso construyendo un contexto estándar a partir de persona, caseta y marca temporal UTC.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        int personaId,
        string? puntoAcceso = null,
        DateTime? fechaHoraUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// Evalúa anomalías de acceso para un contexto completo garantizando respuesta no bloqueante ante cualquier fallo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        ContextoEvaluacionAcceso contexto,
        CancellationToken ct = default);
}
