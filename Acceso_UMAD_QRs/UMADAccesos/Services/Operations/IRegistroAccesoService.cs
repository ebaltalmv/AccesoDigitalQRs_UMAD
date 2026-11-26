using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define las operaciones de control de entradas y salidas en caseta con validación criptográfica y cola offline.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IRegistroAccesoService
{
    /// <summary>
    /// Valida el token de acceso presentado y registra un evento de entrada en la cola local de sincronización.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<RegistroAcceso> RegistrarEntradaAsync(
        int personaId,
        int tokenAccesoId,
        string puntoAcceso,
        int guardiaPersonaId,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default);

    /// <summary>
    /// Registra la salida de una persona vinculándola automáticamente con su entrada abierta más reciente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<RegistroAcceso> RegistrarSalidaAsync(
        int personaId,
        string puntoAcceso,
        int guardiaPersonaId,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la entrada autorizada más reciente de la persona que aún no cuenta con un registro de salida asociado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<RegistroAcceso?> ObtenerEntradaAbiertaAsync(
        int personaId,
        CancellationToken ct = default);
}
