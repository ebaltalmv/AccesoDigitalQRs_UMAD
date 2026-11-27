using UMADAccesos.Models;
using UMADAccesos.Services.AI;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Agrupa el registro de entrada persistido en caseta junto con el resultado del análisis de anomalías y la sugerencia de incidencia.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class RegistroEntradaConAnalisisResult
{
    // Registro de acceso efectivamente persistido y encolado en SQLite.
    public required RegistroAcceso Registro { get; init; }

    // Resultado de la evaluación de anomalías ejecutada de forma no bloqueante.
    public required EvaluacionAnomaliaResult EvaluacionAnomalia { get; init; }

    // Indica si el guardia debería levantar una incidencia ITIL por las alertas detectadas.
    public bool SugerirLevantarIncidencia { get; init; }

    // Severidad ITIL sugerida en caso de escalar la alerta hacia el servicio de incidencias.
    public SeveridadIncidencia SeveridadSugerida { get; init; } = SeveridadIncidencia.Baja;
}
