using Microsoft.Extensions.Logging;
using UMADAccesos.Models;

namespace UMADAccesos.Services.AI;

/// <summary>
/// Orquesta la estrategia de proveedores de IA con degradación controlada a modo offline y protección no bloqueante.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class IaService : IIaService
{
    // Proveedor local offline utilizado de forma primaria o como respaldo automático.
    private readonly IIaProvider? _offlineProvider;

    // Proveedor de producción conectado al backend proxy HTTPS de Azure AI.
    private readonly IIaProvider? _azureProvider;

    // Opciones operativas de IA y reglas de degradación configuradas.
    private readonly IaOptions _options;

    // Registrador de eventos operativos y degradaciones de proveedor.
    private readonly ILogger<IaService> _logger;

    // Modo de proveedor de IA configurado actualmente.
    public ModoProveedorIa ModoActivo => _options.Modo;

    /// <summary>
    /// Inicializa el servicio de IA indexando los proveedores registrados por su modo de ejecución.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public IaService(
        IEnumerable<IIaProvider> providers,
        IaOptions options,
        ILogger<IaService> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        foreach (IIaProvider provider in providers)
        {
            if (provider is null)
            {
                continue;
            }

            if (provider.Modo == ModoProveedorIa.OfflineLocal)
            {
                _offlineProvider = provider;
            }
            else if (provider.Modo == ModoProveedorIa.AzureAiProduction)
            {
                _azureProvider = provider;
            }
        }

        if (_offlineProvider is null && _azureProvider is null)
        {
            throw new InvalidOperationException("Debe registrarse al menos un proveedor de IA (IIaProvider) en el contenedor.");
        }
    }

    /// <summary>
    /// Clasifica un motivo de visita seleccionando el proveedor configurado y degradando a offline si Azure falla.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default)
    {
        if (_options.Modo == ModoProveedorIa.AzureAiProduction)
        {
            if (_azureProvider is not null)
            {
                try
                {
                    return await _azureProvider
                        .ClasificarMotivoVisitaAsync(textoLibre, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Fallo en el proveedor Azure AI al clasificar visita. Degradación offline habilitada: {Degradar}.",
                        _options.Azure.DegradarAOfflineEnFallo);
                }
            }
            else
            {
                _logger.LogWarning(
                    "El modo AzureAiProduction está activo pero no se registró un proveedor AzureAiProduction.");
            }

            if (_options.Azure.DegradarAOfflineEnFallo && _offlineProvider is not null)
            {
                ClasificacionVisitaResult resultadoOffline = await EjecutarClasificacionOfflineSeguraAsync(textoLibre, ct)
                    .ConfigureAwait(false);

                return new ClasificacionVisitaResult
                {
                    TipoVisitaSugerido = resultadoOffline.TipoVisitaSugerido,
                    AreaDestinoSugerida = resultadoOffline.AreaDestinoSugerida,
                    NivelConfianza = resultadoOffline.NivelConfianza,
                    RequiereAprobacionEspecial = resultadoOffline.RequiereAprobacionEspecial,
                    Justificacion = resultadoOffline.Justificacion,
                    ProveedorUtilizado = $"{resultadoOffline.ProveedorUtilizado} (Degradado desde Azure)",
                    PalabrasClaveDetectadas = resultadoOffline.PalabrasClaveDetectadas
                };
            }

            return CrearClasificacionFallbackSeguro(
                "El proveedor remoto de clasificación no está disponible temporalmente. Validar motivo manualmente en caseta.");
        }

        return await EjecutarClasificacionOfflineSeguraAsync(textoLibre, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Evalúa anomalías de acceso construyendo un contexto estándar de entrada para la persona y fecha indicadas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        int personaId,
        string? puntoAcceso = null,
        DateTime? fechaHoraUtc = null,
        CancellationToken ct = default)
    {
        ContextoEvaluacionAcceso contexto = new()
        {
            PersonaId = personaId,
            PuntoAcceso = puntoAcceso,
            TipoMovimiento = TipoMovimientoAcceso.Entrada,
            FechaHoraEventoUtc = fechaHoraUtc ?? DateTime.UtcNow,
            TokenValido = true
        };

        return EvaluarAnomaliaAccesoAsync(contexto, ct);
    }

    /// <summary>
    /// Evalúa anomalías de acceso con degradación automática a offline y captura total de errores para no bloquear caseta.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        ContextoEvaluacionAcceso contexto,
        CancellationToken ct = default)
    {
        if (contexto is null)
        {
            _logger.LogWarning("Se invocó EvaluarAnomaliaAccesoAsync con contexto nulo; se devuelve evaluación no bloqueante.");
            return EvaluacionAnomaliaResult.SinEvaluacionPorFallo(
                "No se recibió contexto de evaluación. Continuar operación aplicando verificación visual estándar.");
        }

        try
        {
            if (_options.Modo == ModoProveedorIa.AzureAiProduction)
            {
                if (_azureProvider is not null)
                {
                    try
                    {
                        return await _azureProvider
                            .EvaluarAnomaliaAsync(contexto, ct)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Fallo en el proveedor Azure AI al evaluar anomalías para PersonaId {PersonaId}. Degradación offline habilitada: {Degradar}.",
                            contexto.PersonaId,
                            _options.Azure.DegradarAOfflineEnFallo);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "El modo AzureAiProduction está activo pero no existe un proveedor AzureAiProduction registrado.");
                }

                if (_options.Azure.DegradarAOfflineEnFallo && _offlineProvider is not null)
                {
                    EvaluacionAnomaliaResult fallbackOffline = await _offlineProvider
                        .EvaluarAnomaliaAsync(contexto, ct)
                        .ConfigureAwait(false);

                    return new EvaluacionAnomaliaResult
                    {
                        EsAnomalo = fallbackOffline.EsAnomalo,
                        NivelRiesgo = fallbackOffline.NivelRiesgo,
                        ReglasActivadas = fallbackOffline.ReglasActivadas,
                        RecomendacionGuardia = fallbackOffline.RecomendacionGuardia,
                        ProveedorUtilizado = $"{fallbackOffline.ProveedorUtilizado} (Degradado desde Azure)",
                        FechaEvaluacionUtc = fallbackOffline.FechaEvaluacionUtc,
                        DegradadoPorFallo = true
                    };
                }

                return EvaluacionAnomaliaResult.SinEvaluacionPorFallo(
                    "Servicio Azure AI no disponible temporalmente. Continuar el flujo de caseta con verificación estándar.");
            }

            if (_offlineProvider is null)
            {
                return EvaluacionAnomaliaResult.SinEvaluacionPorFallo(
                    "Proveedor offline local no disponible. Continuar el flujo de caseta con verificación estándar.");
            }

            return await _offlineProvider
                .EvaluarAnomaliaAsync(contexto, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado durante la evaluación de anomalías para PersonaId {PersonaId}. Se devuelve respuesta no bloqueante.",
                contexto.PersonaId);

            return EvaluacionAnomaliaResult.SinEvaluacionPorFallo(
                "Evaluación de anomalías no disponible por fallo interno. No bloquear acceso; aplicar verificación visual estándar.");
        }
    }

    /// <summary>
    /// Ejecuta la clasificación offline local protegiendo la operación ante errores inesperados.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private async Task<ClasificacionVisitaResult> EjecutarClasificacionOfflineSeguraAsync(
        string textoLibre,
        CancellationToken ct)
    {
        if (_offlineProvider is null)
        {
            return CrearClasificacionFallbackSeguro(
                "No existe un proveedor offline registrado. Clasificar manualmente en caseta.");
        }

        try
        {
            return await _offlineProvider
                .ClasificarMotivoVisitaAsync(textoLibre, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en el proveedor offline al clasificar motivo de visita.");
            return CrearClasificacionFallbackSeguro(
                "No fue posible clasificar el motivo automáticamente. Confirmar área destino y autorización en caseta.");
        }
    }

    /// <summary>
    /// Construye un resultado seguro de clasificación cuando ningún proveedor pudo completar el análisis.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static ClasificacionVisitaResult CrearClasificacionFallbackSeguro(string justificacion)
    {
        return new ClasificacionVisitaResult
        {
            TipoVisitaSugerido = TipoVisitaSugerido.Otro,
            AreaDestinoSugerida = "Recepción General",
            NivelConfianza = 0.0,
            RequiereAprobacionEspecial = true,
            Justificacion = justificacion,
            ProveedorUtilizado = "FallbackSeguro",
            PalabrasClaveDetectadas = Array.Empty<string>()
        };
    }
}
