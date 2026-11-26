using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Administra la creación de pases de evento institucionales y la emisión automática de sus tokens QR asociados.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class PaseEventoService : IPaseEventoService
{
    // Longitud máxima permitida para el nombre del evento.
    private const int LongitudMaximaNombreEvento = 200;

    // Longitud máxima permitida para la descripción opcional del evento.
    private const int LongitudMaximaDescripcion = 500;

    // Fábrica de contextos para persistencia aislada en SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio de seguridad encargado de emitir y firmar tokens de acceso.
    private readonly ITokenAccesoService _tokenAccesoService;

    // Registrador de eventos operativos del módulo de pases por evento.
    private readonly ILogger<PaseEventoService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de pases de evento.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public PaseEventoService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ITokenAccesoService tokenAccesoService,
        ILogger<PaseEventoService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _tokenAccesoService = tokenAccesoService ?? throw new ArgumentNullException(nameof(tokenAccesoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registra un pase de evento en SQLite y genera automáticamente el token <see cref="TipoPase.Evento"/> ajustado a su vigencia.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<PaseEvento> CrearPaseEventoAsync(
        string nombreEvento,
        DateTime inicioUtc,
        DateTime finUtc,
        int personaId,
        int creadoPorPersonaId,
        string? descripcion = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreEvento))
        {
            throw new ArgumentException("El nombre del evento es obligatorio.", nameof(nombreEvento));
        }

        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador del asistente debe ser mayor que cero.");
        }

        if (creadoPorPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creadoPorPersonaId), "El identificador del creador debe ser mayor que cero.");
        }

        DateTime inicioNormalizadoUtc = NormalizarUtc(inicioUtc);
        DateTime finNormalizadoUtc = NormalizarUtc(finUtc);

        if (finNormalizadoUtc <= inicioNormalizadoUtc)
        {
            throw new ArgumentException("La fecha de fin del evento debe ser posterior a la fecha de inicio.", nameof(finUtc));
        }

        DateTime ahoraUtc = DateTime.UtcNow;
        if (finNormalizadoUtc <= ahoraUtc)
        {
            throw new ArgumentException("La fecha de fin del evento debe ser posterior a la fecha y hora actual.", nameof(finUtc));
        }

        string nombreNormalizado = Truncar(nombreEvento.Trim(), LongitudMaximaNombreEvento);
        string? descripcionNormalizada = string.IsNullOrWhiteSpace(descripcion)
            ? null
            : Truncar(descripcion.Trim(), LongitudMaximaDescripcion);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        Persona? asistente = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == personaId, ct)
            .ConfigureAwait(false);

        if (asistente is null)
        {
            throw new InvalidOperationException($"No se encontró la persona asistente con identificador {personaId}.");
        }

        if (!asistente.Activo)
        {
            throw new InvalidOperationException($"La persona asistente con identificador {personaId} no se encuentra activa.");
        }

        bool creadorExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == creadoPorPersonaId, ct)
            .ConfigureAwait(false);

        if (!creadorExiste)
        {
            throw new InvalidOperationException($"No se encontró la persona creadora con identificador {creadoPorPersonaId}.");
        }

        TimeSpan vigenciaToken = finNormalizadoUtc - ahoraUtc;
        TokenAcceso tokenEvento = await _tokenAccesoService
            .EmitirTokenAsync(
                personaId,
                TipoPase.Evento,
                vigenciaToken,
                revocarPreviosActivos: false,
                cancellationToken: ct)
            .ConfigureAwait(false);

        PaseEvento paseEvento = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            NombreEvento = nombreNormalizado,
            Descripcion = descripcionNormalizada,
            FechaInicioEvento = inicioNormalizadoUtc,
            FechaFinEvento = finNormalizadoUtc,
            PersonaId = personaId,
            TokenAccesoId = tokenEvento.Id,
            CreadoPorPersonaId = creadoPorPersonaId,
            FechaCreacionUtc = DateTime.UtcNow,
            Sincronizado = false,
            EstadoSincronizacion = EstadoSincronizacion.Pendiente,
            Reintentos = 0
        };

        db.PasesEvento.Add(paseEvento);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        paseEvento.TokenAcceso = tokenEvento;

        _logger.LogInformation(
            "Pase de evento {PaseEventoId} creado para PersonaId {PersonaId} ({NombreEvento}) con TokenAccesoId {TokenId}.",
            paseEvento.Id,
            personaId,
            nombreNormalizado,
            tokenEvento.Id);

        return paseEvento;
    }

    // Normaliza una fecha a UTC interpretando valores sin especificar como UTC.
    private static DateTime NormalizarUtc(DateTime fecha)
    {
        return fecha.Kind switch
        {
            DateTimeKind.Utc => fecha,
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
        };
    }

    // Acota una cadena a una longitud máxima definida.
    private static string Truncar(string valor, int longitudMaxima)
    {
        return valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
    }
}
