namespace UMADAccesos.Services.Sync;

/// <summary>
/// Resume el resultado de ejecutar un ciclo de procesamiento sobre la cola local de sincronización offline.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SyncProcessResult
{
    // Cantidad total de registros evaluados durante el ciclo de sincronización.
    public int Procesados { get; init; }

    // Cantidad de registros sincronizados correctamente con el destino remoto.
    public int Exitosos { get; init; }

    // Cantidad de registros que fallaron y permanecen disponibles para reintento.
    public int Fallidos { get; init; }

    // Indica si el ciclo no pudo ejecutarse por ausencia de conectividad a internet.
    public bool SinRed { get; init; }

    // Mensaje descriptivo del resultado del ciclo de sincronización.
    public string Mensaje { get; init; } = string.Empty;
}
