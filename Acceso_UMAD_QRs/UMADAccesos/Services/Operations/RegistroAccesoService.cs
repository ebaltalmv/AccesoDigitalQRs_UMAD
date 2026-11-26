using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;
using UMADAccesos.Services.Sync;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Gestiona el registro operativo de entradas y salidas en caseta integrando validación de tokens y cola offline-first.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class RegistroAccesoService : IRegistroAccesoService
{
    // Longitud máxima permitida para el punto de acceso.
    private const int LongitudMaximaPuntoAcceso = 120;

    // Longitud máxima permitida para el motivo de resultado de validación.
    private const int LongitudMaximaMotivoResultado = 300;

    // Longitud máxima permitida para la ruta relativa de evidencia fotográfica.
    private const int LongitudMaximaRutaEvidencia = 500;

    // Longitud máxima permitida para el hash SHA-256 de evidencia fotográfica.
    private const int LongitudMaximaHashEvidencia = 128;

    // Fábrica de contextos para aislamiento de consultas sobre SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio de validación criptográfica HMAC-SHA256 para tokens QR.
    private readonly ITokenAccesoService _tokenAccesoService;

    // Cola de sincronización offline-first para encolar cada movimiento de acceso.
    private readonly ISyncQueueService _syncQueueService;

    // Registrador de eventos operativos de caseta.
    private readonly ILogger<RegistroAccesoService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio operativo de registro de accesos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public RegistroAccesoService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ITokenAccesoService tokenAccesoService,
        ISyncQueueService syncQueueService,
        ILogger<RegistroAccesoService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _tokenAccesoService = tokenAccesoService ?? throw new ArgumentNullException(nameof(tokenAccesoService));
        _syncQueueService = syncQueueService ?? throw new ArgumentNullException(nameof(syncQueueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Valida el token indicado mediante <see cref="ITokenAccesoService.ValidarTokenAsync"/> y encola el registro de entrada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<RegistroAcceso> RegistrarEntradaAsync(
        int personaId,
        int tokenAccesoId,
        string puntoAcceso,
        int guardiaPersonaId,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default)
    {
        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador de persona debe ser mayor que cero.");
        }

        if (tokenAccesoId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenAccesoId), "El identificador de token debe ser mayor que cero.");
        }

        if (guardiaPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guardiaPersonaId), "El identificador del guardia debe ser mayor que cero.");
        }

        string puntoAccesoNormalizado = NormalizarTextoObligatorio(puntoAcceso, nameof(puntoAcceso), LongitudMaximaPuntoAcceso, "El punto de acceso es obligatorio.");
        string? rutaNormalizada = NormalizarTextoOpcional(rutaEvidenciaFoto, LongitudMaximaRutaEvidencia);
        string? hashNormalizado = NormalizarTextoOpcional(hashEvidenciaFoto, LongitudMaximaHashEvidencia);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        bool personaExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == personaId, ct)
            .ConfigureAwait(false);

        if (!personaExiste)
        {
            throw new InvalidOperationException($"No se encontró la persona con identificador {personaId}.");
        }

        bool guardiaExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == guardiaPersonaId, ct)
            .ConfigureAwait(false);

        if (!guardiaExiste)
        {
            throw new InvalidOperationException($"No se encontró el guardia con identificador {guardiaPersonaId}.");
        }

        TokenAcceso? token = await db.TokensAcceso
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tokenAccesoId, ct)
            .ConfigureAwait(false);

        if (token is null)
        {
            throw new InvalidOperationException($"No se encontró el token de acceso con identificador {tokenAccesoId}.");
        }

        TokenValidationResult validacion = await _tokenAccesoService
            .ValidarTokenAsync(token.CodigoFirmado, ct)
            .ConfigureAwait(false);

        bool autorizado = validacion.EsValido && validacion.PersonaId == personaId;
        string motivo = !validacion.EsValido
            ? validacion.Motivo
            : (validacion.PersonaId != personaId
                ? "El token de acceso no pertenece a la persona especificada."
                : validacion.Motivo);

        RegistroAcceso registro = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            PersonaId = personaId,
            TokenAccesoId = token.Id,
            PuntoAcceso = puntoAccesoNormalizado,
            TipoMovimiento = TipoMovimientoAcceso.Entrada,
            Resultado = autorizado ? ResultadoAcceso.Autorizado : ResultadoAcceso.Denegado,
            MotivoResultado = Truncar(motivo, LongitudMaximaMotivoResultado),
            FechaHora = DateTime.UtcNow,
            RutaEvidenciaFoto = rutaNormalizada,
            HashEvidenciaFoto = hashNormalizado,
            GuardiaPersonaId = guardiaPersonaId,
            Sincronizado = false,
            EstadoSincronizacion = EstadoSincronizacion.Pendiente
        };

        RegistroAcceso registroEncolado = await _syncQueueService
            .EncolarRegistroAccesoAsync(registro, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Entrada registrada con resultado {Resultado} (RegistroId {RegistroId}) para PersonaId {PersonaId} por GuardiaId {GuardiaId} en {PuntoAcceso}.",
            registroEncolado.Resultado,
            registroEncolado.Id,
            personaId,
            guardiaPersonaId,
            puntoAccesoNormalizado);

        return registroEncolado;
    }

    /// <summary>
    /// Localiza la entrada abierta de una persona y encola el registro de salida vinculado a dicha entrada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<RegistroAcceso> RegistrarSalidaAsync(
        int personaId,
        string puntoAcceso,
        int guardiaPersonaId,
        CancellationToken ct = default)
    {
        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador de persona debe ser mayor que cero.");
        }

        if (guardiaPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guardiaPersonaId), "El identificador del guardia debe ser mayor que cero.");
        }

        string puntoAccesoNormalizado = NormalizarTextoObligatorio(puntoAcceso, nameof(puntoAcceso), LongitudMaximaPuntoAcceso, "El punto de acceso es obligatorio.");

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        bool guardiaExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == guardiaPersonaId, ct)
            .ConfigureAwait(false);

        if (!guardiaExiste)
        {
            throw new InvalidOperationException($"No se encontró el guardia con identificador {guardiaPersonaId}.");
        }

        RegistroAcceso? entradaAbierta = await ObtenerEntradaAbiertaAsync(personaId, ct).ConfigureAwait(false);
        if (entradaAbierta is null)
        {
            throw new InvalidOperationException($"No se encontró una entrada abierta para la persona con identificador {personaId}.");
        }

        RegistroAcceso salida = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            PersonaId = personaId,
            TokenAccesoId = entradaAbierta.TokenAccesoId,
            PuntoAcceso = puntoAccesoNormalizado,
            TipoMovimiento = TipoMovimientoAcceso.Salida,
            Resultado = ResultadoAcceso.Autorizado,
            MotivoResultado = Truncar($"Salida vinculada a entrada #{entradaAbierta.Id}.", LongitudMaximaMotivoResultado),
            FechaHora = DateTime.UtcNow,
            GuardiaPersonaId = guardiaPersonaId,
            EntradaRelacionadaId = entradaAbierta.Id,
            Sincronizado = false,
            EstadoSincronizacion = EstadoSincronizacion.Pendiente
        };

        RegistroAcceso salidaEncolada = await _syncQueueService
            .EncolarRegistroAccesoAsync(salida, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Salida registrada (RegistroId {RegistroId}) para PersonaId {PersonaId} vinculada a EntradaId {EntradaId} por GuardiaId {GuardiaId}.",
            salidaEncolada.Id,
            personaId,
            entradaAbierta.Id,
            guardiaPersonaId);

        return salidaEncolada;
    }

    /// <summary>
    /// Consulta el último registro de entrada autorizado de una persona que no tiene una salida vinculada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<RegistroAcceso?> ObtenerEntradaAbiertaAsync(
        int personaId,
        CancellationToken ct = default)
    {
        if (personaId <= 0)
        {
            return null;
        }

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.RegistrosAcceso
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.TokenAcceso)
            .Include(r => r.Guardia)
            .Where(r => r.PersonaId == personaId
                && r.TipoMovimiento == TipoMovimientoAcceso.Entrada
                && r.Resultado == ResultadoAcceso.Autorizado
                && !db.RegistrosAcceso.Any(s =>
                    s.TipoMovimiento == TipoMovimientoAcceso.Salida
                    && s.Resultado == ResultadoAcceso.Autorizado
                    && s.EntradaRelacionadaId == r.Id))
            .OrderByDescending(r => r.FechaHora)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    // Valida, recorta y acota una cadena obligatoria a la longitud máxima permitida.
    private static string NormalizarTextoObligatorio(string valor, string parametro, int longitudMaxima, string mensajeVacio)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException(mensajeVacio, parametro);
        }

        return Truncar(valor.Trim(), longitudMaxima);
    }

    // Recorta y acota una cadena opcional devolviendo null si está vacía.
    private static string? NormalizarTextoOpcional(string? valor, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return Truncar(valor.Trim(), longitudMaxima);
    }

    // Acota una cadena a una longitud máxima fija.
    private static string Truncar(string valor, int longitudMaxima)
    {
        return valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
    }
}
