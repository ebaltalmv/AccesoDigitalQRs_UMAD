namespace UMADAccesos.Services.Auth;

/// <summary>
/// Configura los parámetros controlados del entorno de ejecución y vigencia de sesiones bajo políticas ITIL.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class EntornoEjecucionOptions
{
    // Bandera explícita para habilitar el modo de prueba únicamente en compilaciones DEBUG; deshabilitada por defecto.
    public bool HabilitarModoPruebaEnDebug { get; set; } = false;

    // Duración configurada para las sesiones de usuario; se valida dentro de límites seguros en tiempo de ejecución.
    public TimeSpan DuracionSesion { get; set; } = TimeSpan.FromHours(8);
}
