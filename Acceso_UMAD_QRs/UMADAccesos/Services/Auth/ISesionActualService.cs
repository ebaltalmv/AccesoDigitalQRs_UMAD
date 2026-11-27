namespace UMADAccesos.Services.Auth;

/// <summary>
/// Define el contrato del estado reactivo de sesión, autenticación contra SQLite y verificación de permisos del usuario actual.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface ISesionActualService
{
    // Evento reactivo disparado cuando ocurre inicio de sesión, cierre, expiración o cambio de rol en modo de prueba.
    event Action? OnSesionCambiada;

    // Sesión activa actual o nulo si no existe sesión vigente.
    SesionUsuario? SesionActual { get; }

    // Indica si existe una sesión autenticada y vigente en este momento.
    bool EstaAutenticado { get; }

    // Rol operativo RBAC de la sesión vigente o nulo cuando no hay autenticación activa.
    RolUsuario? RolActual { get; }

    /// <summary>
    /// Autentica a una persona activa en SQLite verificando su hash PBKDF2 y establece una sesión con expiración controlada.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<ResultadoAutenticacion> IniciarSesionAsync(string correo, string password, CancellationToken ct = default);

    /// <summary>
    /// Cierra la sesión actual de forma segura y notifica a los suscriptores reactivos.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task CerrarSesionAsync();

    /// <summary>
    /// Verifica si la sesión vigente posee el permiso indicado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool TienePermiso(PermisoSistema permiso);

    /// <summary>
    /// Exige que la sesión vigente cuente con el permiso indicado; lanza UnauthorizedAccessException si no cumple.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    void ExigirPermiso(PermisoSistema permiso);

    /// <summary>
    /// Evalúa si la sesión vigente está autorizada para acceder a los recursos de la persona objetivo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool PuedeAccederRecursosDePersona(int personaObjetivoId);

    /// <summary>
    /// Cambia la sesión a una cuenta demo del rol destino exclusivamente cuando el modo de prueba está habilitado en DEBUG.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<ResultadoAutenticacion> CambiarRolModoPruebaAsync(RolUsuario rolDestino, CancellationToken ct = default);
}
