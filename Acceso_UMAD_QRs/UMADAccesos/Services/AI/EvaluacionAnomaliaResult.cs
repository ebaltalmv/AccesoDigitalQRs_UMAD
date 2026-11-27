namespace UMADAccesos.Services.AI;

/// <summary>
/// Representa el dictamen de evaluación de anomalías para un evento de acceso en caseta.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class EvaluacionAnomaliaResult
{
    // Indica si se activó al menos una regla de comportamiento anómalo.
    public bool EsAnomalo { get; init; }

    // Nivel de riesgo consolidado resultante de las reglas activadas.
    public NivelRiesgoAnomalia NivelRiesgo { get; init; } = NivelRiesgoAnomalia.Bajo;

    // Colección de descripciones claras de las reglas de anomalía disparadas.
    public IReadOnlyList<string> ReglasActivadas { get; init; } = Array.Empty<string>();

    // Indicación operativa accionable dirigida al guardia de caseta.
    public string RecomendacionGuardia { get; init; } = string.Empty;

    // Nombre del proveedor de IA que emitió la evaluación.
    public string ProveedorUtilizado { get; init; } = string.Empty;

    // Fecha y hora UTC en la que se completó la evaluación.
    public DateTime FechaEvaluacionUtc { get; init; } = DateTime.UtcNow;

    // Indica si el resultado provino de una ruta de degradación o contingencia ante fallo.
    public bool DegradadoPorFallo { get; init; }

    /// <summary>
    /// Crea un resultado sin anomalías detectadas para el proveedor especificado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static EvaluacionAnomaliaResult Normal(string proveedor)
    {
        string proveedorNormalizado = string.IsNullOrWhiteSpace(proveedor)
            ? "OfflineLocalHeuristico"
            : proveedor.Trim();

        return new EvaluacionAnomaliaResult
        {
            EsAnomalo = false,
            NivelRiesgo = NivelRiesgoAnomalia.Bajo,
            ReglasActivadas = Array.Empty<string>(),
            RecomendacionGuardia = "Sin anomalías detectadas. Proceder con el flujo estándar de caseta.",
            ProveedorUtilizado = proveedorNormalizado,
            FechaEvaluacionUtc = DateTime.UtcNow,
            DegradadoPorFallo = false
        };
    }

    /// <summary>
    /// Crea un resultado no bloqueante cuando el subsistema de evaluación falla de forma inesperada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static EvaluacionAnomaliaResult SinEvaluacionPorFallo(string detalleSeguro)
    {
        string recomendacion = string.IsNullOrWhiteSpace(detalleSeguro)
            ? "Evaluación automática no disponible temporalmente. Continuar operación aplicando verificación visual estándar."
            : detalleSeguro.Trim();

        return new EvaluacionAnomaliaResult
        {
            EsAnomalo = false,
            NivelRiesgo = NivelRiesgoAnomalia.Bajo,
            ReglasActivadas = Array.Empty<string>(),
            RecomendacionGuardia = recomendacion,
            ProveedorUtilizado = "FallbackSeguro",
            FechaEvaluacionUtc = DateTime.UtcNow,
            DegradadoPorFallo = true
        };
    }
}
