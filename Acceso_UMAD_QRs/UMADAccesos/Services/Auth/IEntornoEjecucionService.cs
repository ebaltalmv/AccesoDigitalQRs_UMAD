namespace UMADAccesos.Services.Auth;

/// <summary>
/// Define el contrato de consulta del entorno de compilación y blindaje del modo de prueba.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IEntornoEjecucionService
{
    // Indica si el binario actual fue compilado en configuración DEBUG.
    bool EsCompilacionDebug { get; }

    // Indica si la bandera explícita del modo de prueba se encuentra activa en DEBUG; siempre falso en RELEASE.
    bool BanderaModoPruebaActiva { get; }

    // Indica si el modo de prueba está efectivamente habilitado; garantizado como falso literal en RELEASE.
    bool EsModoPruebaHabilitado { get; }

    // Nombre descriptivo del entorno activo de ejecución.
    string NombreEntorno { get; }

    // Duración efectiva y validada para la vigencia de sesiones de usuario.
    TimeSpan DuracionSesion { get; }
}
