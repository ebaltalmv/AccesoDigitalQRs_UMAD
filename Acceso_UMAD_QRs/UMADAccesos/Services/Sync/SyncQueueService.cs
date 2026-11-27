using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using UMADAccesos.Data;
using UMADAccesos.Models;

namespace UMADAccesos.Services.Sync;

/// <summary>
/// Implementa la cola local offline-first de registros de acceso para garantizar continuidad operativa en caseta.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SyncQueueService : ISyncQueueService
{
    // Longitud máxima persistida para el detalle del último error de sincronización.
    private const int LongitudMaximaMensajeError = 500;

    // Tamaño de lote aplicado por defecto cuando el solicitante indica un límite no positivo.
    private const int LimiteLotePorDefecto = 50;

    // Fábrica de contextos para aislar cada operación sobre SQLite.
    private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

    // Registrador de eventos operativos de continuidad y sincronización.
    private readonly ILogger<SyncQueueService> _logger;

    // Semáforo estático compartido entre scopes para evitar ciclos concurrentes de despacho sobre los mismos registros.
    private static readonly SemaphoreSlim _procesamientoLock = new(1, 1);

    /// <summary>
    /// Inicializa una nueva instancia del servicio de cola de sincronización offline.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public SyncQueueService(
        IDbContextFactory<UmadDbContext> dbContextFactory,
        ILogger<SyncQueueService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Persiste inmediatamente un registro de acceso en SQLite como pendiente de sincronización sin requerir conexión de red.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<RegistroAcceso> EncolarRegistroAccesoAsync(
        RegistroAcceso registro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        if (registro.IdentificadorGlobal == Guid.Empty)
        {
            registro.IdentificadorGlobal = Guid.NewGuid();
        }

        registro.EstadoSincronizacion = EstadoSincronizacion.Pendiente;
        registro.Sincronizado = false;
        registro.Reintentos = 0;
        registro.FechaUltimoIntento = null;
        registro.UltimoError = null;
        registro.FechaHora = registro.FechaHora == default
            ? DateTime.UtcNow
            : registro.FechaHora.ToUniversalTime();

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.RegistrosAcceso.Add(registro);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Registro de acceso {RegistroId} encolado localmente para PersonaId {PersonaId} en punto {PuntoAcceso}.",
            registro.Id,
            registro.PersonaId,
            registro.PuntoAcceso);

        return registro;
    }

    /// <summary>
    /// Obtiene un lote ordenado cronológicamente de registros pendientes o con error listos para reintento.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<IReadOnlyList<RegistroAcceso>> ObtenerPendientesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        int limiteEfectivo = limite <= 0 ? LimiteLotePorDefecto : limite;

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<RegistroAcceso> pendientes = await dbContext.RegistrosAcceso
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.TokenAcceso)
            .Where(r => !r.Sincronizado
                && (r.EstadoSincronizacion == EstadoSincronizacion.Pendiente
                    || r.EstadoSincronizacion == EstadoSincronizacion.Error))
            .OrderBy(r => r.FechaHora)
            .ThenBy(r => r.Id)
            .Take(limiteEfectivo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pendientes;
    }

    /// <summary>
    /// Actualiza un registro de acceso como sincronizado y limpia cualquier mensaje de error anterior.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task MarcarSincronizadoAsync(
        int registroAccesoId,
        CancellationToken cancellationToken = default)
    {
        if (registroAccesoId <= 0)
        {
            return;
        }

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        RegistroAcceso? registro = await dbContext.RegistrosAcceso
            .FirstOrDefaultAsync(r => r.Id == registroAccesoId, cancellationToken)
            .ConfigureAwait(false);

        if (registro is null)
        {
            _logger.LogWarning("No se encontró el registro de acceso {RegistroId} para marcarlo como sincronizado.", registroAccesoId);
            return;
        }

        registro.Sincronizado = true;
        registro.EstadoSincronizacion = EstadoSincronizacion.Sincronizado;
        registro.FechaUltimoIntento = DateTime.UtcNow;
        registro.UltimoError = null;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registra un fallo de sincronización para un acceso, incrementando reintentos y truncando el error a 500 caracteres.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task RegistrarErrorSincronizacionAsync(
        int registroAccesoId,
        string mensajeError,
        CancellationToken cancellationToken = default)
    {
        if (registroAccesoId <= 0)
        {
            return;
        }

        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        RegistroAcceso? registro = await dbContext.RegistrosAcceso
            .FirstOrDefaultAsync(r => r.Id == registroAccesoId, cancellationToken)
            .ConfigureAwait(false);

        if (registro is null)
        {
            _logger.LogWarning("No se encontró el registro de acceso {RegistroId} para registrar el error de sincronización.", registroAccesoId);
            return;
        }

        string errorNormalizado = string.IsNullOrWhiteSpace(mensajeError)
            ? "Error no especificado durante el envío de sincronización."
            : mensajeError.Trim();

        if (errorNormalizado.Length > LongitudMaximaMensajeError)
        {
            errorNormalizado = errorNormalizado[..LongitudMaximaMensajeError];
        }

        registro.Sincronizado = false;
        registro.EstadoSincronizacion = EstadoSincronizacion.Error;
        registro.Reintentos += 1;
        registro.FechaUltimoIntento = DateTime.UtcNow;
        registro.UltimoError = errorNormalizado;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calcula el conteo actual de registros pendientes, sincronizados, con error y la fecha del último intento UTC.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<SyncQueueSummary> ObtenerResumenColaAsync(CancellationToken cancellationToken = default)
    {
        await using UmadDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        int pendientes = await dbContext.RegistrosAcceso
            .AsNoTracking()
            .CountAsync(r => r.EstadoSincronizacion == EstadoSincronizacion.Pendiente, cancellationToken)
            .ConfigureAwait(false);

        int sincronizados = await dbContext.RegistrosAcceso
            .AsNoTracking()
            .CountAsync(r => r.EstadoSincronizacion == EstadoSincronizacion.Sincronizado, cancellationToken)
            .ConfigureAwait(false);

        int conError = await dbContext.RegistrosAcceso
            .AsNoTracking()
            .CountAsync(r => r.EstadoSincronizacion == EstadoSincronizacion.Error, cancellationToken)
            .ConfigureAwait(false);

        DateTime? ultimoIntento = await dbContext.RegistrosAcceso
            .AsNoTracking()
            .Where(r => r.FechaUltimoIntento.HasValue)
            .MaxAsync(r => r.FechaUltimoIntento, cancellationToken)
            .ConfigureAwait(false);

        int pasesEventoPendientes = await dbContext.PasesEvento
            .AsNoTracking()
            .CountAsync(p => !p.Sincronizado && p.EstadoSincronizacion == EstadoSincronizacion.Pendiente, cancellationToken)
            .ConfigureAwait(false);

        int renovacionesPendientes = await dbContext.SolicitudesRenovacion
            .AsNoTracking()
            .CountAsync(s => !s.Sincronizado && s.EstadoSincronizacion == EstadoSincronizacion.Pendiente, cancellationToken)
            .ConfigureAwait(false);

        int incidenciasPendientes = await dbContext.Incidencias
            .AsNoTracking()
            .CountAsync(i => !i.Sincronizado && i.EstadoSincronizacion == EstadoSincronizacion.Pendiente, cancellationToken)
            .ConfigureAwait(false);

        return new SyncQueueSummary
        {
            Pendientes = pendientes,
            Sincronizados = sincronizados,
            ConError = conError,
            UltimoIntentoUtc = ultimoIntento,
            PasesEventoPendientes = pasesEventoPendientes,
            RenovacionesPendientes = renovacionesPendientes,
            IncidenciasPendientes = incidenciasPendientes
        };
    }

    /// <summary>
    /// Ejecuta de forma exclusiva un ciclo de despacho para los registros pendientes o con error si existe conectividad.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<SyncProcessResult> ProcesarColaPendienteAsync(
        Func<RegistroAcceso, CancellationToken, Task<bool>>? despachadorRemoto = null,
        int loteMaximo = 50,
        CancellationToken cancellationToken = default)
    {
        await _procesamientoLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TieneAccesoAInternet())
            {
                return new SyncProcessResult
                {
                    Procesados = 0,
                    Exitosos = 0,
                    Fallidos = 0,
                    SinRed = true,
                    Mensaje = "Sin acceso a internet. Los registros permanecen resguardados en la cola local."
                };
            }

            if (despachadorRemoto is null)
            {
                return new SyncProcessResult
                {
                    Procesados = 0,
                    Exitosos = 0,
                    Fallidos = 0,
                    SinRed = false,
                    Mensaje = "No hay despachador remoto configurado. Los registros permanecen en espera local."
                };
            }

            IReadOnlyList<RegistroAcceso> lote = await ObtenerPendientesAsync(loteMaximo, cancellationToken)
                .ConfigureAwait(false);

            if (lote.Count == 0)
            {
                return new SyncProcessResult
                {
                    Procesados = 0,
                    Exitosos = 0,
                    Fallidos = 0,
                    SinRed = false,
                    Mensaje = "No hay registros pendientes por sincronizar."
                };
            }

            int procesados = 0;
            int exitosos = 0;
            int fallidos = 0;

            foreach (RegistroAcceso registro in lote)
            {
                cancellationToken.ThrowIfCancellationRequested();
                procesados++;

                try
                {
                    bool enviado = await despachadorRemoto(registro, cancellationToken).ConfigureAwait(false);
                    if (enviado)
                    {
                        await MarcarSincronizadoAsync(registro.Id, cancellationToken).ConfigureAwait(false);
                        exitosos++;
                    }
                    else
                    {
                        await RegistrarErrorSincronizacionAsync(
                                registro.Id,
                                "El destino remoto no confirmó la recepción del registro.",
                                cancellationToken)
                            .ConfigureAwait(false);
                        fallidos++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Fallo al sincronizar el registro de acceso {RegistroId}; quedará disponible para reintento.",
                        registro.Id);

                    await RegistrarErrorSincronizacionAsync(registro.Id, ex.Message, cancellationToken)
                        .ConfigureAwait(false);
                    fallidos++;
                }
            }

            return new SyncProcessResult
            {
                Procesados = procesados,
                Exitosos = exitosos,
                Fallidos = fallidos,
                SinRed = false,
                Mensaje = $"Ciclo completado: {procesados} procesados, {exitosos} exitosos y {fallidos} con error."
            };
        }
        finally
        {
            _procesamientoLock.Release();
        }
    }

    /// <summary>
    /// Consulta la conectividad de red de manera defensiva para tolerar ejecuciones fuera del contexto MAUI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private bool TieneAccesoAInternet()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible consultar el estado de conectividad; se opera en modo sin red.");
            return false;
        }
    }
}
