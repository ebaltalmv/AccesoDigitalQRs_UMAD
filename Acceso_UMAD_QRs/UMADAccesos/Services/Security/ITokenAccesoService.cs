using UMADAccesos.Models;

namespace UMADAccesos.Services.Security;

/// <summary>
/// Define las operaciones del ciclo de vida de los tokens QR firmados con HMAC-SHA256.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface ITokenAccesoService
{
    /// <summary>
    /// Emite y persiste un nuevo token de acceso firmado para una persona activa con vigencia controlada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<TokenAcceso> EmitirTokenAsync(
        int personaId,
        TipoPase tipoPase,
        TimeSpan? vigencia = null,
        bool revocarPreviosActivos = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emite y persiste un token firmado acotado estrictamente a una ventana UTC de inicio y fin (ideal para pases por evento).
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<TokenAcceso> EmitirTokenVentanaAsync(
        int personaId,
        TipoPase tipoPase,
        DateTime inicioVigenciaUtc,
        DateTime finVigenciaUtc,
        bool revocarPreviosActivos = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida la integridad criptográfica, estado de revocación, titularidad y vigencia UTC de un código firmado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<TokenValidationResult> ValidarTokenAsync(
        string codigoFirmado,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca un token existente registrando la fecha UTC y el motivo obligatorio de revocación.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<bool> RevocarTokenAsync(
        int tokenId,
        string motivoRevocacion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca un token existente validando previamente que el operador sea un administrador activo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<bool> RevocarTokenAsync(
        int tokenId,
        int administradorPersonaId,
        string motivoRevocacion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los tokens vigentes y no revocados asociados a una persona específica.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<IReadOnlyList<TokenAcceso>> ObtenerTokensActivosPorPersonaAsync(
        int personaId,
        CancellationToken cancellationToken = default);
}
