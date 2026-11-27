namespace UMADAccesos.Services.Sync;

/// <summary>
/// Consolida las métricas actuales de estado de la cola local de sincronización de registros de acceso.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SyncQueueSummary
{
    // Total de registros de acceso pendientes por enviar al servicio remoto.
    public int Pendientes { get; init; }

    // Total de registros de acceso sincronizados exitosamente.
    public int Sincronizados { get; init; }

    // Total de registros de acceso con error en intentos previos de sincronización.
    public int ConError { get; init; }

    // Fecha y hora UTC del intento de sincronización más reciente registrado.
    public DateTime? UltimoIntentoUtc { get; init; }

    // Total de pases por evento pendientes de sincronización remota.
    public int PasesEventoPendientes { get; init; }

    // Total de solicitudes de renovación pendientes de sincronización remota.
    public int RenovacionesPendientes { get; init; }

    // Total de incidencias operativas pendientes de sincronización remota.
    public int IncidenciasPendientes { get; init; }

    // Conteo consolidado de todas las entidades operativas pendientes de sincronización offline.
    public int TotalPendientesOperativos =>
        Pendientes + PasesEventoPendientes + RenovacionesPendientes + IncidenciasPendientes;
}
