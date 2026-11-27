namespace UMADAccesos.Services.AI;

/// <summary>
/// Define el contrato de estrategia para los proveedores de IA que clasifican visitas y evalúan anomalías de acceso.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IIaProvider
{
    // Modo operativo que representa esta implementación de proveedor.
    ModoProveedorIa Modo { get; }

    // Nombre descriptivo del proveedor registrado en auditoría y resultados.
    string NombreProveedor { get; }

    /// <summary>
    /// Clasifica un motivo de visita expresado en lenguaje natural en español y sugiere el área de destino.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default);

    /// <summary>
    /// Evalúa si un evento de acceso presenta señales de anomalía respecto al historial y reglas institucionales.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAsync(ContextoEvaluacionAcceso contexto, CancellationToken ct = default);
}
