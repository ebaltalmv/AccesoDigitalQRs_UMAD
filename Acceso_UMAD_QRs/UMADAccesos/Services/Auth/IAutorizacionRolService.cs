using UMADAccesos.Models;

namespace UMADAccesos.Services.Auth;

/// <summary>
/// Define el contrato del motor de autorización RBAC para mapear roles, evaluar permisos y verificar propiedad de recursos.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IAutorizacionRolService
{
    /// <summary>
    /// Mapea un rol institucional al rol operativo RBAC correspondiente bajo política fail-closed.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    RolUsuario MapearRolUsuario(RolPersona rolPersona);

    /// <summary>
    /// Obtiene el conjunto inmutable de permisos concedidos al rol operativo indicado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    IReadOnlySet<PermisoSistema> ObtenerPermisos(RolUsuario rol);

    /// <summary>
    /// Obtiene el conjunto inmutable de permisos concedidos al rol institucional indicado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    IReadOnlySet<PermisoSistema> ObtenerPermisos(RolPersona rolPersona);

    /// <summary>
    /// Verifica si un rol operativo cuenta con el permiso específico solicitado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool TienePermiso(RolUsuario rol, PermisoSistema permiso);

    /// <summary>
    /// Verifica si un rol institucional cuenta con el permiso específico solicitado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool TienePermiso(RolPersona rolPersona, PermisoSistema permiso);

    /// <summary>
    /// Determina si la sesión proporcionada puede acceder a los recursos pertenecientes a la persona objetivo.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool PuedeAccederRecursosDePersona(SesionUsuario? sesion, int personaObjetivoId);

    /// <summary>
    /// Exige que la sesión sea válida, vigente y cuente con el permiso indicado; lanza excepción de seguridad en caso contrario.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    void ExigirPermiso(SesionUsuario? sesion, PermisoSistema permiso);
}
