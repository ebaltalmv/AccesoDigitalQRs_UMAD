namespace UMADAccesos.Services.Auth;

/// <summary>
/// Define los roles operativos de autorización utilizados por el control de acceso basado en roles (RBAC) de UMAD Accesos.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public enum RolUsuario
{
    // Integrante de la comunidad UMAD (estudiante, docente, administrativo o visitante) con acceso únicamente a sus propios recursos.
    UsuarioComunidad = 1,

    // Personal de seguridad en caseta autorizado para validar accesos, capturar evidencia y reportar incidencias operativas.
    GuardiaSeguridad = 2,

    // Administrador de accesos autorizado para gestionar renovaciones, eventos, revocaciones, incidencias y auditoría global.
    AdministradorAccesos = 3
}
