using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Define el contrato de creación de pases temporales para eventos institucionales con emisión automática de token QR.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IPaseEventoService
{
    /// <summary>
    /// Crea un pase de evento y emite automáticamente su token criptográfico de tipo <see cref="TipoPase.Evento"/>.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<PaseEvento> CrearPaseEventoAsync(
        string nombreEvento,
        DateTime inicioUtc,
        DateTime finUtc,
        int personaId,
        int creadoPorPersonaId,
        string? descripcion = null,
        CancellationToken ct = default);
}
