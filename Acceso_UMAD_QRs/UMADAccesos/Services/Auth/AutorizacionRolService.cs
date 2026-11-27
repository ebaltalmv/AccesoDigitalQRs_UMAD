using System.Collections.Frozen;
using UMADAccesos.Models;

namespace UMADAccesos.Services.Auth;

/// <summary>
/// Implementa la matriz verificable de autorización RBAC bajo el principio de mínimo privilegio y política fail-closed.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class AutorizacionRolService : IAutorizacionRolService
{
    // Conjunto vacío inmutable devuelto en escenarios fail-closed ante roles desconocidos.
    private static readonly FrozenSet<PermisoSistema> PermisosVacios = FrozenSet<PermisoSistema>.Empty;

    // Matriz estática, inmutable y estrictamente disjunta de permisos por rol operativo.
    private static readonly FrozenDictionary<RolUsuario, IReadOnlySet<PermisoSistema>> MatrizPermisos =
        new Dictionary<RolUsuario, IReadOnlySet<PermisoSistema>>
        {
            [RolUsuario.UsuarioComunidad] = new[]
            {
                PermisoSistema.ConsultarPropiosPases,
                PermisoSistema.SolicitarPaseORenovacion,
                PermisoSistema.VerPropioHistorial,
                PermisoSistema.ClasificarMotivoVisitaIa
            }.ToFrozenSet(),

            [RolUsuario.GuardiaSeguridad] = new[]
            {
                PermisoSistema.EscanearCodigoQr,
                PermisoSistema.CapturarEvidenciaFoto,
                PermisoSistema.RegistrarEntradaSalida,
                PermisoSistema.VerAlertasAnomalia,
                PermisoSistema.ReportarIncidencia
            }.ToFrozenSet(),

            [RolUsuario.AdministradorAccesos] = new[]
            {
                PermisoSistema.AprobarRechazarRenovaciones,
                PermisoSistema.GestionarPasesEvento,
                PermisoSistema.RevocarTokens,
                PermisoSistema.ConsultarHistorialGlobal,
                PermisoSistema.GestionarIncidencias,
                PermisoSistema.SupervisarSincronizacionOffline
            }.ToFrozenSet()
        }.ToFrozenDictionary();

    /// <summary>
    /// Mapea un rol institucional al rol operativo correspondiente; rechaza valores desconocidos con excepción fail-closed.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public RolUsuario MapearRolUsuario(RolPersona rolPersona)
    {
        return rolPersona switch
        {
            RolPersona.Estudiante => RolUsuario.UsuarioComunidad,
            RolPersona.Docente => RolUsuario.UsuarioComunidad,
            RolPersona.Administrativo => RolUsuario.UsuarioComunidad,
            RolPersona.Visitante => RolUsuario.UsuarioComunidad,
            RolPersona.Guardia => RolUsuario.GuardiaSeguridad,
            RolPersona.Administrador => RolUsuario.AdministradorAccesos,
            _ => throw new ArgumentOutOfRangeException(
                nameof(rolPersona),
                rolPersona,
                "El rol institucional especificado no cuenta con una asignación operativa válida.")
        };
    }

    /// <summary>
    /// Devuelve los permisos estrictos concedidos al rol operativo especificado o un conjunto vacío si el rol no es reconocido.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public IReadOnlySet<PermisoSistema> ObtenerPermisos(RolUsuario rol)
    {
        if (!Enum.IsDefined(rol))
        {
            return PermisosVacios;
        }

        return MatrizPermisos.TryGetValue(rol, out IReadOnlySet<PermisoSistema>? permisos)
            ? permisos
            : PermisosVacios;
    }

    /// <summary>
    /// Devuelve los permisos concedidos al rol institucional especificado bajo política fail-closed.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public IReadOnlySet<PermisoSistema> ObtenerPermisos(RolPersona rolPersona)
    {
        if (!Enum.IsDefined(rolPersona))
        {
            return PermisosVacios;
        }

        RolUsuario rolOperativo = MapearRolUsuario(rolPersona);
        return ObtenerPermisos(rolOperativo);
    }

    /// <summary>
    /// Evalúa si el rol operativo indicado posee explícitamente el permiso solicitado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool TienePermiso(RolUsuario rol, PermisoSistema permiso)
    {
        if (!Enum.IsDefined(rol) || !Enum.IsDefined(permiso))
        {
            return false;
        }

        return MatrizPermisos.TryGetValue(rol, out IReadOnlySet<PermisoSistema>? permisos)
            && permisos.Contains(permiso);
    }

    /// <summary>
    /// Evalúa si el rol institucional indicado posee explícitamente el permiso solicitado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool TienePermiso(RolPersona rolPersona, PermisoSistema permiso)
    {
        if (!Enum.IsDefined(rolPersona) || !Enum.IsDefined(permiso))
        {
            return false;
        }

        RolUsuario rolOperativo = MapearRolUsuario(rolPersona);
        return TienePermiso(rolOperativo, permiso);
    }

    /// <summary>
    /// Valida la propiedad de recursos por persona: deniega sesiones nulas o expiradas, permite acceso global al administrador y limita a comunidad y guardia a su propia identidad.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool PuedeAccederRecursosDePersona(SesionUsuario? sesion, int personaObjetivoId)
    {
        if (personaObjetivoId <= 0 || sesion is null || sesion.PersonaId <= 0 || sesion.EstaExpirada())
        {
            return false;
        }

        return sesion.RolOperativo switch
        {
            RolUsuario.AdministradorAccesos => true,
            RolUsuario.UsuarioComunidad => sesion.PersonaId == personaObjetivoId,
            RolUsuario.GuardiaSeguridad => sesion.PersonaId == personaObjetivoId,
            _ => false
        };
    }

    /// <summary>
    /// Exige que la sesión esté activa, vigente y autorizada para el permiso solicitado; lanza UnauthorizedAccessException con mensaje genérico seguro al fallar.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public void ExigirPermiso(SesionUsuario? sesion, PermisoSistema permiso)
    {
        if (sesion is null || sesion.EstaExpirada())
        {
            throw new UnauthorizedAccessException("La sesión no es válida o ha expirado. Inicie sesión nuevamente.");
        }

        if (!TienePermiso(sesion.RolOperativo, permiso))
        {
            throw new UnauthorizedAccessException("No cuenta con autorización para ejecutar esta operación.");
        }
    }
}
