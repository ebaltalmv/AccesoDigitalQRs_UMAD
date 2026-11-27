using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UMADAccesos.Services.AI;

/// <summary>
/// Cliente HTTP tipado que se comunica exclusivamente vía HTTPS con el backend proxy institucional de Azure AI.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class AzureAiHttpClient : IAzureAiClient
{
    // Ruta relativa del endpoint proxy para clasificación de motivos de visita.
    private const string RutaClasificacionVisita = "api/v1/ai/classify-visit";

    // Ruta relativa del endpoint proxy para evaluación de anomalías de acceso.
    private const string RutaEvaluacionAnomalia = "api/v1/ai/evaluate-anomaly";

    // Longitud máxima de texto libre enviada al backend proxy para acotar carga útil.
    private const int LongitudMaximaTexto = 500;

    // Opciones de serialización JSON compatibles con contratos REST camelCase.
    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    // Cliente HTTP inyectado para reutilizar conexiones y facilitar pruebas unitarias.
    private readonly HttpClient _httpClient;

    // Opciones generales de IA que incluyen la configuración no secreta del backend proxy.
    private readonly IaOptions _options;

    // Registrador estructurado libre de secretos o datos sensibles.
    private readonly ILogger<AzureAiHttpClient> _logger;

    // Indica si existe una URL base HTTPS válida configurada para invocar el backend proxy.
    public bool EstaConfigurado => _options.Azure.EstaConfiguradoHttps();

    /// <summary>
    /// Inicializa una nueva instancia del cliente HTTP hacia el backend proxy de Azure AI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public AzureAiHttpClient(
        HttpClient httpClient,
        IaOptions options,
        ILogger<AzureAiHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Envía el texto libre de la visita al backend proxy HTTPS y deserializa la clasificación de Azure AI.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default)
    {
        Uri endpoint = ConstruirEndpointSeguro(RutaClasificacionVisita);
        string textoAcotado = NormalizarTextoEnvio(textoLibre);

        SolicitudClasificacionProxy solicitud = new()
        {
            DeploymentName = _options.Azure.DeploymentName,
            TextoLibre = textoAcotado,
            Idioma = "es-MX"
        };

        using CancellationTokenSource timeoutCts = CrearTimeoutVinculado(ct);
        using HttpResponseMessage respuesta = await _httpClient
            .PostAsJsonAsync(endpoint, solicitud, JsonOpciones, timeoutCts.Token)
            .ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "El backend proxy de Azure AI respondió con estado HTTP {StatusCode} al clasificar motivo de visita.",
                (int)respuesta.StatusCode);
            respuesta.EnsureSuccessStatusCode();
        }

        ClasificacionVisitaResult? resultado = await respuesta.Content
            .ReadFromJsonAsync<ClasificacionVisitaResult>(JsonOpciones, timeoutCts.Token)
            .ConfigureAwait(false);

        if (resultado is null)
        {
            throw new InvalidOperationException("El backend proxy de Azure AI devolvió una respuesta de clasificación vacía.");
        }

        return new ClasificacionVisitaResult
        {
            TipoVisitaSugerido = Enum.IsDefined(resultado.TipoVisitaSugerido)
                ? resultado.TipoVisitaSugerido
                : TipoVisitaSugerido.Otro,
            AreaDestinoSugerida = string.IsNullOrWhiteSpace(resultado.AreaDestinoSugerida)
                ? "Recepción General"
                : resultado.AreaDestinoSugerida.Trim(),
            NivelConfianza = Math.Round(Math.Clamp(resultado.NivelConfianza, 0.0, 1.0), 2),
            RequiereAprobacionEspecial = resultado.RequiereAprobacionEspecial,
            Justificacion = string.IsNullOrWhiteSpace(resultado.Justificacion)
                ? "Clasificación generada mediante backend proxy de Azure AI."
                : resultado.Justificacion.Trim(),
            ProveedorUtilizado = string.IsNullOrWhiteSpace(resultado.ProveedorUtilizado)
                ? "AzureAiProduction"
                : resultado.ProveedorUtilizado.Trim(),
            PalabrasClaveDetectadas = resultado.PalabrasClaveDetectadas ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Envía el contexto minimizado del evento de acceso al backend proxy HTTPS y obtiene el dictamen de anomalías.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAsync(ContextoEvaluacionAcceso contexto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        Uri endpoint = ConstruirEndpointSeguro(RutaEvaluacionAnomalia);

        SolicitudAnomaliaProxy solicitud = new()
        {
            DeploymentName = _options.Azure.DeploymentName,
            PersonaId = contexto.PersonaId,
            PuntoAcceso = string.IsNullOrWhiteSpace(contexto.PuntoAcceso) ? null : contexto.PuntoAcceso.Trim(),
            TipoMovimiento = contexto.TipoMovimiento.ToString(),
            FechaHoraEventoUtc = contexto.FechaHoraEventoUtc == default
                ? DateTime.UtcNow
                : contexto.FechaHoraEventoUtc.ToUniversalTime(),
            TokenAccesoId = contexto.TokenAccesoId,
            TokenValido = contexto.TokenValido,
            MotivoValidacionToken = NormalizarTextoOpcional(contexto.MotivoValidacionToken, 200),
            RegistroAccesoActualId = contexto.RegistroAccesoActualId
        };

        using CancellationTokenSource timeoutCts = CrearTimeoutVinculado(ct);
        using HttpResponseMessage respuesta = await _httpClient
            .PostAsJsonAsync(endpoint, solicitud, JsonOpciones, timeoutCts.Token)
            .ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "El backend proxy de Azure AI respondió con estado HTTP {StatusCode} al evaluar anomalías para PersonaId {PersonaId}.",
                (int)respuesta.StatusCode,
                contexto.PersonaId);
            respuesta.EnsureSuccessStatusCode();
        }

        EvaluacionAnomaliaResult? resultado = await respuesta.Content
            .ReadFromJsonAsync<EvaluacionAnomaliaResult>(JsonOpciones, timeoutCts.Token)
            .ConfigureAwait(false);

        if (resultado is null)
        {
            throw new InvalidOperationException("El backend proxy de Azure AI devolvió una respuesta de anomalías vacía.");
        }

        return new EvaluacionAnomaliaResult
        {
            EsAnomalo = resultado.EsAnomalo,
            NivelRiesgo = Enum.IsDefined(resultado.NivelRiesgo)
                ? resultado.NivelRiesgo
                : NivelRiesgoAnomalia.Bajo,
            ReglasActivadas = resultado.ReglasActivadas ?? Array.Empty<string>(),
            RecomendacionGuardia = string.IsNullOrWhiteSpace(resultado.RecomendacionGuardia)
                ? "Seguir protocolo estándar de verificación en caseta."
                : resultado.RecomendacionGuardia.Trim(),
            ProveedorUtilizado = string.IsNullOrWhiteSpace(resultado.ProveedorUtilizado)
                ? "AzureAiProduction"
                : resultado.ProveedorUtilizado.Trim(),
            FechaEvaluacionUtc = resultado.FechaEvaluacionUtc == default
                ? DateTime.UtcNow
                : resultado.FechaEvaluacionUtc,
            DegradadoPorFallo = resultado.DegradadoPorFallo
        };
    }

    /// <summary>
    /// Valida estrictamente el esquema HTTPS de la URL base configurada y compone el endpoint destino.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private Uri ConstruirEndpointSeguro(string rutaRelativa)
    {
        if (!_options.Azure.EstaConfiguradoHttps())
        {
            throw new InvalidOperationException(
                "El backend proxy de Azure AI no está configurado o no utiliza un esquema HTTPS válido.");
        }

        string baseNormalizada = _options.Azure.BackendProxyBaseUrl!.Trim();
        if (!baseNormalizada.EndsWith("/", StringComparison.Ordinal))
        {
            baseNormalizada += "/";
        }

        Uri baseUri = new(baseNormalizada, UriKind.Absolute);
        if (!baseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permiten comunicaciones HTTPS hacia el backend proxy de Azure AI.");
        }

        return new Uri(baseUri, rutaRelativa);
    }

    /// <summary>
    /// Crea un token de cancelación enlazado con el límite de tiempo configurado para cada llamada HTTP.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private CancellationTokenSource CrearTimeoutVinculado(CancellationToken ct)
    {
        int segundos = Math.Clamp(_options.Azure.TimeoutSegundos, 1, 60);
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(segundos));
        return cts;
    }

    /// <summary>
    /// Recorta y acota el texto libre antes de enviarlo al backend proxy.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string NormalizarTextoEnvio(string textoLibre)
    {
        if (string.IsNullOrWhiteSpace(textoLibre))
        {
            return string.Empty;
        }

        string limpio = textoLibre.Trim();
        return limpio.Length <= LongitudMaximaTexto ? limpio : limpio[..LongitudMaximaTexto];
    }

    /// <summary>
    /// Normaliza una cadena opcional acotando su longitud máxima permitida.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static string? NormalizarTextoOpcional(string? texto, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string limpio = texto.Trim();
        return limpio.Length <= longitudMaxima ? limpio : limpio[..longitudMaxima];
    }

    /// <summary>
    /// Representa la carga útil JSON mínima enviada al proxy para clasificar el motivo de una visita.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private sealed class SolicitudClasificacionProxy
    {
        // Nombre lógico del despliegue de modelo en Azure AI.
        public string DeploymentName { get; init; } = string.Empty;

        // Texto libre declarado por el visitante sin datos personales sensibles.
        public string TextoLibre { get; init; } = string.Empty;

        // Código de idioma esperado para la inferencia lingüística.
        public string Idioma { get; init; } = "es-MX";
    }

    /// <summary>
    /// Representa la carga útil JSON pseudonimizada enviada al proxy para evaluar anomalías de acceso.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private sealed class SolicitudAnomaliaProxy
    {
        // Nombre lógico del despliegue de evaluación en Azure AI.
        public string DeploymentName { get; init; } = string.Empty;

        // Identificador numérico interno sin datos nominales ni correo.
        public int PersonaId { get; init; }

        // Punto de acceso o caseta donde ocurrió el evento.
        public string? PuntoAcceso { get; init; }

        // Dirección del movimiento en formato textual.
        public string TipoMovimiento { get; init; } = string.Empty;

        // Marca de tiempo UTC del evento de acceso.
        public DateTime FechaHoraEventoUtc { get; init; }

        // Identificador opcional del token evaluado.
        public int? TokenAccesoId { get; init; }

        // Indica si el token presentado pasó validación local.
        public bool TokenValido { get; init; }

        // Motivo técnico no sensible de la validación del token.
        public string? MotivoValidacionToken { get; init; }

        // Identificador opcional del registro recién persistido para evitar doble conteo.
        public int? RegistroAccesoActualId { get; init; }
    }
}
