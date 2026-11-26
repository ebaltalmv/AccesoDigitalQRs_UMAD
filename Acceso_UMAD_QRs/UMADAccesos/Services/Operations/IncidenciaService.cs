using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Data;
using UMADAccesos.Models;

namespace UMADAccesos.Services.Operations;

/// <summary>
/// Implementa la gestión local de incidencias en caseta conforme al flujo de clasificación, evidencia y seguimiento ITIL 4.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class IncidenciaService : IIncidenciaService
{
    // Longitud máxima permitida para el título de la incidencia.
    private const int LongitudMaximaTitulo = 200;

    // Longitud máxima permitida para la descripción de la incidencia.
    private const int LongitudMaximaDescripcion = 2000;

    // Longitud máxima permitida para la ruta relativa de evidencia fotográfica.
    private const int LongitudMaximaRutaEvidencia = 500;

    // Longitud máxima permitida para el hash SHA-256 de evidencia fotográfica.
    private const int LongitudMaximaHashEvidencia = 128;

    // Fábrica de contextos para aislamiento de operaciones en SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Registrador de eventos operativos de gestión de incidencias.
    private readonly ILogger<IncidenciaService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de incidencias.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public IncidenciaService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ILogger<IncidenciaService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registra una nueva incidencia con severidad ITIL 4, vínculo opcional al evento de acceso y evidencia fotográfica.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<Incidencia> ReportarIncidenciaAsync(
        int reportadoPorPersonaId,
        SeveridadIncidencia severidad,
        string titulo,
        string descripcion,
        int? registroAccesoId = null,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default)
    {
        if (reportadoPorPersonaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reportadoPorPersonaId), "El identificador del reportante debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(severidad))
        {
            throw new ArgumentOutOfRangeException(nameof(severidad), "La severidad indicada no es válida.");
        }

        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("El título de la incidencia es obligatorio.", nameof(titulo));
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ArgumentException("La descripción de la incidencia es obligatoria.", nameof(descripcion));
        }

        if (registroAccesoId.HasValue && registroAccesoId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registroAccesoId), "El identificador del registro de acceso debe ser mayor que cero.");
        }

        string tituloNormalizado = Truncar(titulo.Trim(), LongitudMaximaTitulo);
        string descripcionNormalizada = Truncar(descripcion.Trim(), LongitudMaximaDescripcion);
        string? rutaNormalizada = NormalizarTextoOpcional(rutaEvidenciaFoto, LongitudMaximaRutaEvidencia);
        string? hashNormalizado = NormalizarTextoOpcional(hashEvidenciaFoto, LongitudMaximaHashEvidencia);

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        bool reportanteExiste = await db.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Id == reportadoPorPersonaId, ct)
            .ConfigureAwait(false);

        if (!reportanteExiste)
        {
            throw new InvalidOperationException($"No se encontró la persona reportante con identificador {reportadoPorPersonaId}.");
        }

        if (registroAccesoId.HasValue)
        {
            bool registroExiste = await db.RegistrosAcceso
                .AsNoTracking()
                .AnyAsync(r => r.Id == registroAccesoId.Value, ct)
                .ConfigureAwait(false);

            if (!registroExiste)
            {
                throw new InvalidOperationException($"No se encontró el registro de acceso con identificador {registroAccesoId.Value}.");
            }
        }

        Incidencia incidencia = new()
        {
            IdentificadorGlobal = Guid.NewGuid(),
            ReportadoPorPersonaId = reportadoPorPersonaId,
            RegistroAccesoId = registroAccesoId,
            Severidad = severidad,
            Estado = EstadoIncidencia.Abierta,
            Titulo = tituloNormalizado,
            Descripcion = descripcionNormalizada,
            RutaEvidenciaFoto = rutaNormalizada,
            HashEvidenciaFoto = hashNormalizado,
            FechaReporteUtc = DateTime.UtcNow,
            Sincronizado = false,
            EstadoSincronizacion = EstadoSincronizacion.Pendiente,
            Reintentos = 0
        };

        db.Incidencias.Add(incidencia);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Incidencia {IncidenciaId} reportada con severidad {Severidad} por PersonaId {ReportadoPorId}.",
            incidencia.Id,
            severidad,
            reportadoPorPersonaId);

        return incidencia;
    }

    /// <summary>
    /// Actualiza el estado operativo de una incidencia registrando la fecha de resolución cuando aplica.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<Incidencia> ActualizarEstadoAsync(
        int incidenciaId,
        EstadoIncidencia nuevoEstado,
        CancellationToken ct = default)
    {
        if (incidenciaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incidenciaId), "El identificador de la incidencia debe ser mayor que cero.");
        }

        if (!Enum.IsDefined(nuevoEstado))
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoEstado), "El estado indicado no es válido.");
        }

        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        Incidencia? incidencia = await db.Incidencias
            .FirstOrDefaultAsync(i => i.Id == incidenciaId, ct)
            .ConfigureAwait(false);

        if (incidencia is null)
        {
            throw new InvalidOperationException($"No se encontró la incidencia con identificador {incidenciaId}.");
        }

        incidencia.Estado = nuevoEstado;
        if (nuevoEstado is EstadoIncidencia.Resuelta or EstadoIncidencia.Cerrada)
        {
            incidencia.FechaResolucionUtc ??= DateTime.UtcNow;
        }
        else
        {
            incidencia.FechaResolucionUtc = null;
        }

        incidencia.Sincronizado = false;
        incidencia.EstadoSincronizacion = EstadoSincronizacion.Pendiente;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Incidencia {IncidenciaId} actualizada a estado {NuevoEstado}.",
            incidencia.Id,
            nuevoEstado);

        return incidencia;
    }

    /// <summary>
    /// Obtiene las incidencias que permanecen abiertas o en investigación, priorizadas por severidad descendente.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<Incidencia>> ObtenerAbiertasAsync(CancellationToken ct = default)
    {
        await using UmadDbContext db = await _dbContextFactory
            .CreateDbContextAsync(ct)
            .ConfigureAwait(false);

        return await db.Incidencias
            .AsNoTracking()
            .Include(i => i.ReportadoPor)
            .Include(i => i.RegistroAcceso)
            .Where(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnInvestigacion)
            .OrderByDescending(i => i.Severidad)
            .ThenBy(i => i.FechaReporteUtc)
            .ThenBy(i => i.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
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

    // Acota una cadena a una longitud máxima definida.
    private static string Truncar(string valor, int longitudMaxima)
    {
        return valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
    }
}
