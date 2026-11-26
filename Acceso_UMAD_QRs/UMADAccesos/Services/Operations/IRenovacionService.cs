using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define el flujo de solicitud, aprobación, rechazo y consulta de renovaciones de pases digitales.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IRenovacionService
{
    /// <summary>
    /// Registra una nueva solicitud de renovación de pase digital en estado pendiente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<SolicitudRenovacion> SolicitarRenovacionAsync(
        int personaId,
        TipoPase tipoPaseDeseado,
        string justificacion,
        TimeSpan? vigenciaSolicitada = null,
        CancellationToken ct = default);

    /// <summary>
    /// Aprueba una solicitud pendiente, emite el nuevo token de acceso y lo vincula a la solicitud.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<SolicitudRenovacion> AprobarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        CancellationToken ct = default);

    /// <summary>
    /// Rechaza una solicitud pendiente documentando el motivo de resolución y el responsable.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<SolicitudRenovacion> RechazarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        string motivoRechazo,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la lista de solicitudes de renovación en estado pendiente ordenadas cronológicamente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<SolicitudRenovacion>> ObtenerPendientesAsync(
        CancellationToken ct = default);
}
