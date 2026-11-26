using UMADAccesos.Models;

namespace UMADAccesos.Services.Security;

/// <summary>
/// Representa el resultado detallado de validar un token de acceso firmado criptográficamente.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class TokenValidationResult
{
    // Indica si el token superó todas las validaciones criptográficas y de negocio.
    public bool EsValido { get; init; }

    // Descripción clara del resultado de la validación o causa del rechazo.
    public string Motivo { get; init; } = string.Empty;

    // Entidad de token encontrada en la base de datos local cuando existe coincidencia.
    public TokenAcceso? Token { get; init; }

    // Identificador de la persona titular asociada al token evaluado.
    public int? PersonaId { get; init; }

    // Clasificación del pase correspondiente al token evaluado.
    public TipoPase? TipoPase { get; init; }

    // Fecha y hora de expiración en formato UTC asociada al token evaluado.
    public DateTime? FechaExpiracionUtc { get; init; }

    /// <summary>
    /// Crea un resultado de validación exitoso a partir de un token de acceso verificado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static TokenValidationResult Valido(TokenAcceso token)
    {
        ArgumentNullException.ThrowIfNull(token);

        DateTime expiracionUtc = token.FechaExpiracion.Kind == DateTimeKind.Utc
            ? token.FechaExpiracion
            : DateTime.SpecifyKind(token.FechaExpiracion, DateTimeKind.Utc);

        return new TokenValidationResult
        {
            EsValido = true,
            Motivo = "Token de acceso válido.",
            Token = token,
            PersonaId = token.PersonaId,
            TipoPase = token.TipoPase,
            FechaExpiracionUtc = expiracionUtc
        };
    }

    /// <summary>
    /// Crea un resultado de validación fallido indicando el motivo del rechazo y opcionalmente el token localizado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public static TokenValidationResult Invalido(string motivo, TokenAcceso? token = null)
    {
        string motivoNormalizado = string.IsNullOrWhiteSpace(motivo)
            ? "El token de acceso no es válido."
            : motivo.Trim();

        DateTime? expiracionUtc = token is null
            ? null
            : (token.FechaExpiracion.Kind == DateTimeKind.Utc
                ? token.FechaExpiracion
                : DateTime.SpecifyKind(token.FechaExpiracion, DateTimeKind.Utc));

        return new TokenValidationResult
        {
            EsValido = false,
            Motivo = motivoNormalizado,
            Token = token,
            PersonaId = token?.PersonaId,
            TipoPase = token?.TipoPase,
            FechaExpiracionUtc = expiracionUtc
        };
    }
}
