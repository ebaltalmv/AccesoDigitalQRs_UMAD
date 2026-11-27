using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;

namespace UMADAccesos.Services.Security;

/// <summary>
/// Administra la emisión, validación criptográfica HMAC-SHA256 y revocación de tokens de acceso digitales.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class TokenAccesoService : ITokenAccesoService
{
    // Versión del formato estructurado de carga útil firmada.
    private const string VersionPayload = "v2";

    // Separador de campos dentro de la carga útil estructurada.
    private const char SeparadorPayload = '|';

    // Separador entre la carga útil codificada y su firma HMAC-SHA256.
    private const char SeparadorFirma = '.';

    // Longitud en bytes del nonce aleatorio incluido en cada emisión.
    private const int LongitudNonceBytes = 16;

    // Longitud esperada en bytes de una firma HMAC-SHA256.
    private const int LongitudFirmaSha256Bytes = 32;

    // Motivo estándar aplicado al revocar tokens previos cuando se emite uno nuevo.
    private const string MotivoReemplazoEmision = "Reemplazado por nueva emisión";

    // Fábrica de contextos para acceso aislado y seguro a la base de datos SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Proveedor de clave simétrica de firma criptográfica.
    private readonly ITokenKeyProvider _tokenKeyProvider;

    // Registrador de eventos operativos de seguridad.
    private readonly ILogger<TokenAccesoService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de gestión y verificación de tokens de acceso.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public TokenAccesoService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ITokenKeyProvider tokenKeyProvider,
        ILogger<TokenAccesoService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(tokenKeyProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _tokenKeyProvider = tokenKeyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Genera, firma con HMAC-SHA256 y almacena un nuevo token de acceso para una persona activa.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<TokenAcceso> EmitirTokenAsync(
        int personaId,
        TipoPase tipoPase,
        TimeSpan? vigencia = null,
        bool revocarPreviosActivos = true,
        CancellationToken cancellationToken = default)
    {
        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador de persona debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(tipoPase))
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPase), "El tipo de pase especificado no es válido.");
        }

        if (vigencia.HasValue && vigencia.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(vigencia), "La vigencia del token debe ser mayor que cero.");
        }

        long emisionUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TimeSpan duracion = vigencia ?? ObtenerVigenciaPorDefecto(tipoPase);
        long segundosVigencia = Math.Max(1L, (long)Math.Ceiling(duracion.TotalSeconds));
        long expiracionUnix = emisionUnix + segundosVigencia;

        return await EmitirTokenInternoAsync(
                personaId,
                tipoPase,
                emisionUnix,
                expiracionUnix,
                revocarPreviosActivos,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Emite y firma con HMAC-SHA256 un token acotado estrictamente a la ventana UTC [inicioVigenciaUtc, finVigenciaUtc].
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<TokenAcceso> EmitirTokenVentanaAsync(
        int personaId,
        TipoPase tipoPase,
        DateTime inicioVigenciaUtc,
        DateTime finVigenciaUtc,
        bool revocarPreviosActivos = true,
        CancellationToken cancellationToken = default)
    {
        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador de persona debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(tipoPase))
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPase), "El tipo de pase especificado no es válido.");
        }

        DateTime inicioNormalizadoUtc = NormalizarUtc(inicioVigenciaUtc);
        DateTime finNormalizadoUtc = NormalizarUtc(finVigenciaUtc);

        if (finNormalizadoUtc <= inicioNormalizadoUtc)
        {
            throw new ArgumentException("La fecha de fin de vigencia debe ser posterior a la fecha de inicio.", nameof(finVigenciaUtc));
        }

        if (finNormalizadoUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("La fecha de fin de vigencia debe ser posterior a la fecha y hora actual.", nameof(finVigenciaUtc));
        }

        long emisionUnix = new DateTimeOffset(inicioNormalizadoUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        long expiracionUnix = new DateTimeOffset(finNormalizadoUtc, TimeSpan.Zero).ToUnixTimeSeconds();

        if (expiracionUnix <= emisionUnix)
        {
            expiracionUnix = emisionUnix + 1L;
        }

        return await EmitirTokenInternoAsync(
                personaId,
                tipoPase,
                emisionUnix,
                expiracionUnix,
                revocarPreviosActivos,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Persiste y firma criptográficamente el token para una persona activa usando las marcas Unix exactas de inicio y expiración.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async Task<TokenAcceso> EmitirTokenInternoAsync(
        int personaId,
        TipoPase tipoPase,
        long emisionUnix,
        long expiracionUnix,
        bool revocarPreviosActivos,
        CancellationToken cancellationToken)
    {
        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Persona? persona = await dbContext.Personas
            .FirstOrDefaultAsync(p => p.Id == personaId, cancellationToken)
            .ConfigureAwait(false);

        if (persona is null)
        {
            throw new InvalidOperationException($"No se encontró la persona con identificador {personaId}.");
        }

        if (!persona.Activo)
        {
            throw new InvalidOperationException($"No se puede emitir un token porque la persona {personaId} está inactiva.");
        }

        DateTime ahoraUtc = DateTime.UtcNow;
        DateTime fechaEmisionUtc = DateTimeOffset.FromUnixTimeSeconds(emisionUnix).UtcDateTime;
        DateTime fechaExpiracionUtc = DateTimeOffset.FromUnixTimeSeconds(expiracionUnix).UtcDateTime;

        if (revocarPreviosActivos)
        {
            List<TokenAcceso> tokensActivosPrevios = await dbContext.TokensAcceso
                .Where(t => t.PersonaId == personaId && !t.Revocado && t.FechaExpiracion >= ahoraUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (TokenAcceso tokenPrevio in tokensActivosPrevios)
            {
                tokenPrevio.Revocado = true;
                tokenPrevio.FechaRevocacionUtc = ahoraUtc;
                tokenPrevio.MotivoRevocacion = MotivoReemplazoEmision;
            }
        }

        Guid identificadorGlobal = Guid.NewGuid();
        string nonceHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(LongitudNonceBytes)).ToLowerInvariant();
        string payload = string.Join(
            SeparadorPayload,
            VersionPayload,
            identificadorGlobal.ToString("N"),
            persona.Id.ToString(CultureInfo.InvariantCulture),
            ((int)tipoPase).ToString(CultureInfo.InvariantCulture),
            emisionUnix.ToString(CultureInfo.InvariantCulture),
            expiracionUnix.ToString(CultureInfo.InvariantCulture),
            nonceHex);

        string payloadBase64Url = CodificarBase64Url(Encoding.UTF8.GetBytes(payload));
        string firmaBase64Url = await CalcularFirmaBase64UrlAsync(payloadBase64Url, cancellationToken).ConfigureAwait(false);
        string codigoFirmado = $"{payloadBase64Url}{SeparadorFirma}{firmaBase64Url}";

        TokenAcceso nuevoToken = new()
        {
            IdentificadorGlobal = identificadorGlobal,
            PersonaId = persona.Id,
            CodigoFirmado = codigoFirmado,
            TipoPase = tipoPase,
            FechaEmision = fechaEmisionUtc,
            FechaExpiracion = fechaExpiracionUtc,
            Revocado = false,
            FechaRevocacionUtc = null,
            MotivoRevocacion = null,
            Persona = persona
        };

        dbContext.TokensAcceso.Add(nuevoToken);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Token de acceso {TokenId} emitido para PersonaId {PersonaId} con TipoPase {TipoPase} vigente de {FechaEmisionUtc:O} a {FechaExpiracionUtc:O}.",
            nuevoToken.Id,
            nuevoToken.PersonaId,
            nuevoToken.TipoPase,
            nuevoToken.FechaEmision,
            nuevoToken.FechaExpiracion);

        return nuevoToken;
    }

    /// <summary>
    /// Verifica el formato, firma HMAC-SHA256 en tiempo constante, persistencia, estado del titular y vigencia UTC del token.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<TokenValidationResult> ValidarTokenAsync(
        string codigoFirmado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoFirmado))
        {
            return TokenValidationResult.Invalido("El código de acceso proporcionado está vacío.");
        }

        string codigoNormalizado = codigoFirmado.Trim();
        string[] segmentos = codigoNormalizado.Split(SeparadorFirma);
        if (segmentos.Length != 2
            || string.IsNullOrWhiteSpace(segmentos[0])
            || string.IsNullOrWhiteSpace(segmentos[1]))
        {
            return TokenValidationResult.Invalido("El formato del token de acceso es inválido.");
        }

        string payloadBase64Url = segmentos[0];
        string firmaBase64Url = segmentos[1];

        if (!IntentarDecodificarBase64Url(firmaBase64Url, out byte[]? firmaRecibida)
            || firmaRecibida is null
            || firmaRecibida.Length != LongitudFirmaSha256Bytes)
        {
            return TokenValidationResult.Invalido("La firma del token no presenta un formato válido.");
        }

        bool firmaValida = await VerificarFirmaConstanteAsync(payloadBase64Url, firmaRecibida, cancellationToken)
            .ConfigureAwait(false);

        if (!firmaValida)
        {
            _logger.LogWarning("Intento de validación rechazado por firma HMAC-SHA256 inválida.");
            return TokenValidationResult.Invalido("La firma criptográfica del token no es válida.");
        }

        if (!IntentarDecodificarBase64Url(payloadBase64Url, out byte[]? payloadBytes) || payloadBytes is null)
        {
            return TokenValidationResult.Invalido("No fue posible decodificar la carga útil del token.");
        }

        string payloadTexto = Encoding.UTF8.GetString(payloadBytes);
        if (!IntentarParsearPayload(
            payloadTexto,
            out Guid guidPayload,
            out int personaIdPayload,
            out TipoPase tipoPasePayload,
            out DateTime emisionPayloadUtc,
            out DateTime expiracionPayloadUtc))
        {
            return TokenValidationResult.Invalido("La estructura interna del token es inválida.");
        }

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TokenAcceso? token = await dbContext.TokensAcceso
            .AsNoTracking()
            .Include(t => t.Persona)
            .FirstOrDefaultAsync(
                t => t.IdentificadorGlobal == guidPayload && t.CodigoFirmado == codigoNormalizado,
                cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            return TokenValidationResult.Invalido("El token no existe en el registro local de accesos.");
        }

        if (token.PersonaId != personaIdPayload)
        {
            return TokenValidationResult.Invalido("El titular indicado en el token no coincide con el registro local.", token);
        }

        if (token.TipoPase != tipoPasePayload)
        {
            return TokenValidationResult.Invalido("El tipo de pase del token no coincide con el registro local.", token);
        }

        if (token.Revocado)
        {
            string motivoRevocacion = string.IsNullOrWhiteSpace(token.MotivoRevocacion)
                ? "El token se encuentra revocado."
                : $"El token fue revocado: {token.MotivoRevocacion}";
            return TokenValidationResult.Invalido(motivoRevocacion, token);
        }

        if (token.Persona is null || !token.Persona.Activo)
        {
            return TokenValidationResult.Invalido("La persona titular del token no se encuentra activa.", token);
        }

        DateTime ahoraUtc = DateTime.UtcNow;
        DateTime emisionDbUtc = NormalizarUtc(token.FechaEmision);
        DateTime expiracionDbUtc = NormalizarUtc(token.FechaExpiracion);

        if (ahoraUtc < emisionDbUtc || ahoraUtc < emisionPayloadUtc)
        {
            return TokenValidationResult.Invalido("El token aún no inicia su periodo de vigencia.", token);
        }

        if (ahoraUtc > expiracionDbUtc || ahoraUtc > expiracionPayloadUtc)
        {
            return TokenValidationResult.Invalido("El token de acceso ha expirado.", token);
        }

        return TokenValidationResult.Valido(token);
    }

    /// <summary>
    /// Marca un token de acceso como revocado dejando trazabilidad de fecha UTC y motivo obligatorio.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<bool> RevocarTokenAsync(
        int tokenId,
        string motivoRevocacion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(motivoRevocacion))
        {
            throw new ArgumentException("El motivo de revocación es obligatorio.", nameof(motivoRevocacion));
        }

        if (tokenId <= 0)
        {
            return false;
        }

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TokenAcceso? token = await dbContext.TokensAcceso
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken)
            .ConfigureAwait(false);

        if (token is null || token.Revocado)
        {
            return false;
        }

        token.Revocado = true;
        token.FechaRevocacionUtc = DateTime.UtcNow;
        token.MotivoRevocacion = motivoRevocacion.Trim();

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token de acceso {TokenId} revocado correctamente.", token.Id);
        return true;
    }

    /// <summary>
    /// Recupera todos los tokens vigentes y no revocados pertenecientes a la persona indicada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<TokenAcceso>> ObtenerTokensActivosPorPersonaAsync(
        int personaId,
        CancellationToken cancellationToken = default)
    {
        if (personaId <= 0)
        {
            return Array.Empty<TokenAcceso>();
        }

        DateTime ahoraUtc = DateTime.UtcNow;

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TokenAcceso> tokens = await dbContext.TokensAcceso
            .AsNoTracking()
            .Where(t => t.PersonaId == personaId
                && !t.Revocado
                && t.FechaEmision <= ahoraUtc
                && t.FechaExpiracion >= ahoraUtc)
            .OrderByDescending(t => t.FechaEmision)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tokens;
    }

    // Determina la vigencia segura por defecto según el tipo de pase institucional.
    private static TimeSpan ObtenerVigenciaPorDefecto(TipoPase tipoPase) => tipoPase switch
    {
        TipoPase.Visitante => TimeSpan.FromHours(12),
        TipoPase.Evento => TimeSpan.FromHours(24),
        TipoPase.Temporal => TimeSpan.FromDays(7),
        TipoPase.Permanente => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(12)
    };

    // Calcula la firma HMAC-SHA256 en formato Base64Url para la carga útil codificada.
    private async Task<string> CalcularFirmaBase64UrlAsync(string payloadBase64Url, CancellationToken cancellationToken)
    {
        byte[] clave = await _tokenKeyProvider.ObtenerClaveFirmaAsync(cancellationToken).ConfigureAwait(false);
        byte[]? firma = null;

        try
        {
            byte[] datos = Encoding.UTF8.GetBytes(payloadBase64Url);
            firma = HMACSHA256.HashData(clave, datos);
            return CodificarBase64Url(firma);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
            if (firma is not null)
            {
                CryptographicOperations.ZeroMemory(firma);
            }
        }
    }

    // Compara la firma recibida contra la firma esperada en tiempo constante.
    private async Task<bool> VerificarFirmaConstanteAsync(
        string payloadBase64Url,
        byte[] firmaRecibida,
        CancellationToken cancellationToken)
    {
        byte[] clave = await _tokenKeyProvider.ObtenerClaveFirmaAsync(cancellationToken).ConfigureAwait(false);
        byte[]? firmaEsperada = null;

        try
        {
            byte[] datos = Encoding.UTF8.GetBytes(payloadBase64Url);
            firmaEsperada = HMACSHA256.HashData(clave, datos);
            return CryptographicOperations.FixedTimeEquals(firmaEsperada, firmaRecibida);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
            if (firmaEsperada is not null)
            {
                CryptographicOperations.ZeroMemory(firmaEsperada);
            }
        }
    }

    // Analiza y valida cada campo de la carga útil estructurada del token v2.
    private static bool IntentarParsearPayload(
        string payload,
        out Guid identificadorGlobal,
        out int personaId,
        out TipoPase tipoPase,
        out DateTime emisionUtc,
        out DateTime expiracionUtc)
    {
        identificadorGlobal = Guid.Empty;
        personaId = 0;
        tipoPase = default;
        emisionUtc = default;
        expiracionUtc = default;

        string[] partes = payload.Split(SeparadorPayload);
        if (partes.Length != 7)
        {
            return false;
        }

        if (!string.Equals(partes[0], VersionPayload, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Guid.TryParse(partes[1], out identificadorGlobal) || identificadorGlobal == Guid.Empty)
        {
            return false;
        }

        if (!int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out personaId) || personaId <= 0)
        {
            return false;
        }

        if (!int.TryParse(partes[3], NumberStyles.None, CultureInfo.InvariantCulture, out int tipoPaseValor)
            || !Enum.IsDefined(typeof(TipoPase), tipoPaseValor))
        {
            return false;
        }

        if (!long.TryParse(partes[4], NumberStyles.None, CultureInfo.InvariantCulture, out long emisionUnix)
            || !long.TryParse(partes[5], NumberStyles.None, CultureInfo.InvariantCulture, out long expiracionUnix)
            || expiracionUnix <= emisionUnix)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(partes[6]))
        {
            return false;
        }

        try
        {
            emisionUtc = DateTimeOffset.FromUnixTimeSeconds(emisionUnix).UtcDateTime;
            expiracionUtc = DateTimeOffset.FromUnixTimeSeconds(expiracionUnix).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        tipoPase = (TipoPase)tipoPaseValor;
        return true;
    }

    // Codifica un arreglo de bytes en representación Base64Url sin relleno.
    private static string CodificarBase64Url(byte[] datos)
    {
        return Convert.ToBase64String(datos)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // Decodifica una cadena Base64Url validando su longitud y caracteres permitidos.
    private static bool IntentarDecodificarBase64Url(string entrada, out byte[]? datos)
    {
        datos = null;
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return false;
        }

        int residuo = entrada.Length % 4;
        if (residuo == 1)
        {
            return false;
        }

        string base64 = entrada.Replace('-', '+').Replace('_', '/');
        if (residuo == 2)
        {
            base64 += "==";
        }
        else if (residuo == 3)
        {
            base64 += "=";
        }

        try
        {
            datos = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Garantiza que una marca temporal recuperada de SQLite se interprete explícitamente como UTC.
    private static DateTime NormalizarUtc(DateTime fecha)
    {
        return fecha.Kind switch
        {
            DateTimeKind.Utc => fecha,
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
        };
    }
}
