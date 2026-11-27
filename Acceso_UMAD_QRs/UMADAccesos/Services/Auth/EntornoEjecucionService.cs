namespace UMADAccesos.Services.Auth;

/// <summary>
/// Provee el control estricto de entorno y blindaje en tiempo de compilación del modo de prueba conforme a control de cambios ITIL.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class EntornoEjecucionService : IEntornoEjecucionService
{
    // Límite inferior permitido para la vigencia de una sesión autenticada.
    private static readonly TimeSpan DuracionMinimaSesion = TimeSpan.FromMinutes(15);

    // Límite superior permitido para la vigencia de una sesión autenticada.
    private static readonly TimeSpan DuracionMaximaSesion = TimeSpan.FromHours(24);

    // Duración predeterminada aplicada cuando la configuración recibida no es positiva.
    private static readonly TimeSpan DuracionPorDefectoSesion = TimeSpan.FromHours(8);

#if DEBUG
    // Estado inmutable de la bandera de modo de prueba evaluado únicamente en compilaciones DEBUG.
    private readonly bool _banderaModoPruebaEnDebug;
#endif

    // Duración validada y acotada para todas las sesiones emitidas en el entorno actual.
    private readonly TimeSpan _duracionSesion;

    /// <summary>
    /// Inicializa el servicio de entorno validando la duración de sesión y aislando el modo de prueba a compilaciones DEBUG.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public EntornoEjecucionService(EntornoEjecucionOptions? options = null)
    {
        EntornoEjecucionOptions configuracion = options ?? new EntornoEjecucionOptions();

#if DEBUG
        _banderaModoPruebaEnDebug = configuracion.HabilitarModoPruebaEnDebug;
#endif

        _duracionSesion = NormalizarDuracionSesion(configuracion.DuracionSesion);
    }

#if DEBUG
    // En compilación DEBUG identifica el entorno como de desarrollo.
    public bool EsCompilacionDebug => true;

    // En compilación DEBUG refleja la activación explícita de la bandera de modo de prueba.
    public bool BanderaModoPruebaActiva => _banderaModoPruebaEnDebug;

    // En compilación DEBUG exige simultáneamente compilación de desarrollo y bandera explícita activa.
    public bool EsModoPruebaHabilitado => EsCompilacionDebug && BanderaModoPruebaActiva;

    // Etiqueta descriptiva del entorno de desarrollo.
    public string NombreEntorno => "Desarrollo (DEBUG)";
#else
    // En compilación RELEASE siempre retorna falso para impedir comportamientos de desarrollo.
    public bool EsCompilacionDebug => false;

    // En compilación RELEASE la bandera de modo de prueba permanece desactivada de forma literal.
    public bool BanderaModoPruebaActiva => false;

    // En compilación RELEASE el modo de prueba es estrictamente falso literal.
    public bool EsModoPruebaHabilitado => false;

    // Etiqueta descriptiva del entorno productivo.
    public string NombreEntorno => "Producción (RELEASE)";
#endif

    // Devuelve la duración validada de sesión comprendida estrictamente entre 15 minutos y 24 horas.
    public TimeSpan DuracionSesion => _duracionSesion;

    /// <summary>
    /// Valida y acota la duración de sesión configurada para garantizar valores positivos entre 15 minutos y 24 horas.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    private static TimeSpan NormalizarDuracionSesion(TimeSpan duracionSolicitada)
    {
        if (duracionSolicitada <= TimeSpan.Zero)
        {
            return DuracionPorDefectoSesion;
        }

        if (duracionSolicitada < DuracionMinimaSesion)
        {
            return DuracionMinimaSesion;
        }

        if (duracionSolicitada > DuracionMaximaSesion)
        {
            return DuracionMaximaSesion;
        }

        return duracionSolicitada;
    }
}
