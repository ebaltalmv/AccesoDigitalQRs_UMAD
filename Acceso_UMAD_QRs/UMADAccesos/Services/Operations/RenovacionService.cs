using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Administra el ciclo completo de solicitudes de renovación de pases digitales con emisión automática al aprobar.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class RenovacionService : IRenovacionService
{
    // Longitud máxima permitida para justificación y motivo de resolución.
    private const int LongitudMaximaTexto = 500;

    // Fábrica de contextos para acceso aislado a SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Servicio de emisión criptográfica de nuevos tokens tras aprobación.
    private readonly ITokenAccesoService _tokenAccesoService;

    // Registrador de eventos operativos del flujo de renovaciones.
    private readonly ILogger<RenovacionService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de renovaciones de pases.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public RenovacionService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ITokenAccesoService tokenAccesoService,
        ILogger<RenovacionService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _tokenAccesoService = tokenAccesoService ?? throw new ArgumentNullException(nameof(tokenAccesoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Crea y persiste una solicitud de renovación pendiente vinculándola con el último token conocido de la persona.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<SolicitudRenovacion> SolicitarRenovacionAsync(
        int personaId,
        TipoPase tipoPaseDeseado,
        string justificacion,
        TimeSpan? vigenciaSolicitada = null,
        CancellationToken ct = default)
    {
        if (personaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personaId), "El identificador de persona debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(tipoPaseDeseado))
        {
            throw new ArgumentOutOfRangeException(nameof(tipoPaseDeseado), "El tipo de pase solicitado no es válido.");
        }

        if (string.IsNullOrWhiteSpace(justificacion))
        {
            throw new ArgumentException("La justificación de la solicitud es obligatoria.", nameof(justificacion));
        }

        if (vigenciaSolicitada.HasValue && vigenciaSolicitada.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(vigenciaSolicitada), "La vigencia solicitada debe ser mayor que cero.");
        }

        string justificacionNormalizada = Truncar(justificacion.Trim(), LongitudMaximaTexto);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        bool personaExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == personaId && p.Activo, ct)
            .ConfigureAwait(false);

        if (!personaExiste)
        {
            throw new InvalidOperationException($"No se encontró una persona activa con identificador {personaId}.");
        }

        int? tokenOriginalId = await db.TokensAcceso
            .AsNoTracking()
            .Where(t => t.PersonaId == personaId)
            .OrderByDescending(t => !t.Revocado)
            .ThenByDescending(t => t.FechaExpiracion)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        SolicitudRenovacion solicitud = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            PersonaId = personaId,
            TokenAccesoOriginalId = tokenOriginalId,
            TipoPaseDeseado = tipoPaseDeseado,
            VigenciaSolicitada = vigenciaSolicitada,
            Justificacion = justificacionNormalizada,
            Estado = EstadoRenovacion.Pendiente,
            FechaSolicitudUtc = DateTime.UtcNow,
            Sincronizado = false,
            EstadoSincronizacion = EstadoSincronizacion.Pendiente,
            Reintentos = 0
        };

        db.SolicitudesRenovacion.Add(solicitud);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Solicitud de renovación {SolicitudId} registrada para PersonaId {PersonaId} solicitando TipoPase {TipoPase}.",
            solicitud.Id,
            personaId,
            tipoPaseDeseado);

        return solicitud;
    }

    /// <summary>
    /// Aprueba una solicitud pendiente, emite un nuevo token mediante <see cref="ITokenAccesoService.EmitirTokenAsync"/> y actualiza la trazabilidad.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<SolicitudRenovacion> AprobarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        CancellationToken ct = default)
    {
        if (solicitudId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(solicitudId), "El identificador de la solicitud debe ser mayor que cero.");
        }

        if (resueltoPorPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resueltoPorPersonaId), "El identificador del responsable debe ser mayor que cero.");
        }

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        SolicitudRenovacion? solicitud = await db.SolicitudesRenovacion
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            .ConfigureAwait(false);

        if (solicitud is null)
        {
            throw new InvalidOperationException($"No se encontró la solicitud de renovación con identificador {solicitudId}.");
        }

        if (solicitud.Estado != EstadoRenovacion.Pendiente)
        {
            throw new InvalidOperationException($"La solicitud {solicitudId} no se encuentra pendiente (estado actual: {solicitud.Estado}).");
        }

        if (resueltoPorPersonaId == solicitud.PersonaId)
        {
            throw new UnauthorizedAccessException("Un usuario no puede aprobar su propia solicitud de renovación.");
        }

        Persona? responsable = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == resueltoPorPersonaId, ct)
            .ConfigureAwait(false);

        if (responsable is null || !responsable.Activo)
        {
            throw new InvalidOperationException($"No se encontró un responsable activo con identificador {resueltoPorPersonaId}.");
        }

        if (responsable.Rol != RolPersona.Administrador)
        {
            throw new UnauthorizedAccessException("Solo un administrador de accesos activo puede aprobar solicitudes de renovación.");
        }

        TokenAcceso nuevoToken = await _tokenAccesoService
            .EmitirTokenAsync(
                solicitud.PersonaId,
                solicitud.TipoPaseDeseado,
                solicitud.VigenciaSolicitada,
                revocarPreviosActivos: true,
                cancellationToken: ct)
            .ConfigureAwait(false);

        solicitud.Estado = EstadoRenovacion.Aprobada;
        solicitud.ResueltoPorPersonaId = resueltoPorPersonaId;
        solicitud.TokenAccesoNuevoId = nuevoToken.Id;
        solicitud.FechaResolucionUtc = DateTime.UtcNow;
        solicitud.Sincronizado = false;
        solicitud.EstadoSincronizacion = EstadoSincronizacion.Pendiente;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        solicitud.TokenAccesoNuevo = nuevoToken;

        _logger.LogInformation(
            "Solicitud de renovación {SolicitudId} aprobada por PersonaId {ResponsableId}; nuevo TokenAccesoId {TokenId} emitido.",
            solicitud.Id,
            resueltoPorPersonaId,
            nuevoToken.Id);

        return solicitud;
    }

    /// <summary>
    /// Rechaza una solicitud pendiente dejando constancia del motivo y del usuario que resolvió el trámite.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<SolicitudRenovacion> RechazarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        string motivoRechazo,
        CancellationToken ct = default)
    {
        if (solicitudId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(solicitudId), "El identificador de la solicitud debe ser mayor que cero.");
        }

        if (resueltoPorPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resueltoPorPersonaId), "El identificador del responsable debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivoRechazo));
        }

        string motivoNormalizado = Truncar(motivoRechazo.Trim(), LongitudMaximaTexto);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        SolicitudRenovacion? solicitud = await db.SolicitudesRenovacion
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            .ConfigureAwait(false);

        if (solicitud is null)
        {
            throw new InvalidOperationException($"No se encontró la solicitud de renovación con identificador {solicitudId}.");
        }

        if (solicitud.Estado != EstadoRenovacion.Pendiente)
        {
            throw new InvalidOperationException($"La solicitud {solicitudId} no se encuentra pendiente (estado actual: {solicitud.Estado}).");
        }

        if (resueltoPorPersonaId == solicitud.PersonaId)
        {
            throw new UnauthorizedAccessException("Un usuario no puede rechazar su propia solicitud de renovación.");
        }

        Persona? responsable = await db.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == resueltoPorPersonaId, ct)
            .ConfigureAwait(false);

        if (responsable is null || !responsable.Activo)
        {
            throw new InvalidOperationException($"No se encontró un responsable activo con identificador {resueltoPorPersonaId}.");
        }

        if (responsable.Rol != RolPersona.Administrador)
        {
            throw new UnauthorizedAccessException("Solo un administrador de accesos activo puede rechazar solicitudes de renovación.");
        }

        solicitud.Estado = EstadoRenovacion.Rechazada;
        solicitud.ResueltoPorPersonaId = resueltoPorPersonaId;
        solicitud.MotivoResolucion = motivoNormalizado;
        solicitud.FechaResolucionUtc = DateTime.UtcNow;
        solicitud.Sincronizado = false;
        solicitud.EstadoSincronizacion = EstadoSincronizacion.Pendiente;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Solicitud de renovación {SolicitudId} rechazada por PersonaId {ResponsableId}.",
            solicitud.Id,
            resueltoPorPersonaId);

        return solicitud;
    }

    /// <summary>
    /// Recupera todas las solicitudes de renovación que continúan pendientes de resolución.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<SolicitudRenovacion>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.SolicitudesRenovacion
            .AsNoTracking()
            .Include(s => s.Persona)
            .Include(s => s.TokenAccesoOriginal)
            .Where(s => s.Estado == EstadoRenovacion.Pendiente)
            .OrderBy(s => s.FechaSolicitudUtc)
            .ThenBy(s => s.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acota una cadena de texto a una longitud máxima fija.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string Truncar(string valor, int longitudMaxima)
    {
        return valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
    }
}
