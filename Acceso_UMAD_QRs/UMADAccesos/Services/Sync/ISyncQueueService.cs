using UMADAccesos.Models;

namespace UMADAccesos.Services.Sync;

/// <summary>
/// Define el contrato de la cola offline-first para encolar, monitorear y sincronizar registros de acceso.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface ISyncQueueService
{
    /// <summary>
    /// Registra de forma local e inmediata un movimiento de acceso en estado pendiente de sincronización.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<RegistroAcceso> EncolarRegistroAccesoAsync(
        RegistroAcceso registro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera los registros pendientes o con error reintentable ordenados cronológicamente para su despacho.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<RegistroAcceso>> ObtenerPendientesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un registro de acceso como sincronizado exitosamente limpiando cualquier error previo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task MarcarSincronizadoAsync(
        int registroAccesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un fallo de sincronización incrementando el contador de reintentos y almacenando el detalle del error.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task RegistrarErrorSincronizacionAsync(
        int registroAccesoId,
        string mensajeError,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un resumen agregado con los conteos por estado y el último intento de sincronización registrado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<SyncQueueSummary> ObtenerResumenColaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Procesa un lote de registros pendientes cuando existe red y un despachador remoto disponible.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<SyncProcessResult> ProcesarColaPendienteAsync(
        Func<RegistroAcceso, CancellationToken, Task<bool>>? despachadorRemoto = null,
        int loteMaximo = 50,
        CancellationToken cancellationToken = default);
}
