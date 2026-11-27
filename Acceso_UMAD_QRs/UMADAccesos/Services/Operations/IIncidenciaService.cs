using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define las operaciones de gestión de incidencias en caseta alineadas con prácticas ITIL 4 Incident Management.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IIncidenciaService
{
    /// <summary>
    /// Registra una nueva incidencia operativa o de seguridad con clasificación de severidad y evidencia opcional.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<Incidencia> ReportarIncidenciaAsync(
        int reportadoPorPersonaId,
        SeveridadIncidencia severidad,
        string titulo,
        string descripcion,
        int? registroAccesoId = null,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza el estado dentro del ciclo de atención de una incidencia existente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<Incidencia> ActualizarEstadoAsync(
        int incidenciaId,
        EstadoIncidencia nuevoEstado,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza el estado de una incidencia verificando que el operador responsable sea un administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<Incidencia> ActualizarEstadoAsync(
        int incidenciaId,
        EstadoIncidencia nuevoEstado,
        int administradorPersonaId,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene las incidencias activas (abiertas o en investigación) priorizadas por severidad y antigüedad.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<Incidencia>> ObtenerAbiertasAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene el listado completo de incidencias con filtro opcional por estado, ordenado por severidad y fecha.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<Incidencia>> ObtenerTodasAsync(
        EstadoIncidencia? filtroEstado = null,
        CancellationToken ct = default);
}
